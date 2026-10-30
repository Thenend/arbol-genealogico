using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using ArbolGenealogico.Movil.Dibujo;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// Imprimir el árbol (como la guía de impresión del escritorio): en cuántas hojas y de qué papel (A4 a A1, o los de Estados
/// Unidos), con qué colores, y cómo de grande saldrá la letra. Arriba se ven las hojas ya pegadas, con el árbol como saldrá.
/// Se imprime con el sistema del teléfono o se comparte el PDF de las hojas (para imprimirlo en otro sitio).
/// En vez de las hojas para pegar se puede hacer una lámina para regalar (Ramo completo): el árbol entero en una hoja, con un
/// título, una dedicatoria y el marco de un estilo (ver <see cref="Lamina"/>).
/// </summary>
public sealed class PantallaImpresion : Grid
{
    private readonly Arbol _arbol;
    private readonly FormaTarjeta _forma;
    private readonly Ordenacion _orden;
    private readonly bool _marcaHistoria;
    private readonly string _nombre;
    private readonly Dictionary<(EstiloPapel, Dibujo.Tema, Letra), LienzoArbol> _lienzos = new();

    private Papel _papel;
    private int _hojas;
    private EstiloPapel _estilo;
    /// <summary>El estilo del árbol y la letra de las tarjetas con los que se imprime (empiezan como en la pantalla).</summary>
    private Dibujo.Tema _tema;
    private Letra _letra;
    private bool _evitarCortes;
    private bool _destacar;
    private bool _lamina;
    private EstiloLamina _estiloLamina;
    private string _titulo, _dedicatoria;
    /// <summary>La foto de fondo de la lámina (null: sin foto) y su intensidad.</summary>
    private Avalonia.Media.Imaging.Bitmap? _foto;
    private double _opacidad;

    private readonly VistaHojas _vista = new();
    private readonly VistaLamina _vistaLamina = new() { IsVisible = false };
    /// <summary>El título y la dedicatoria de la lámina: los mismos cuadros siempre (al escribir no se rehacen los ajustes).</summary>
    private readonly TextBox _cajaTitulo, _cajaDedicatoria;
    private readonly Control _pista, _entera;
    private readonly StackPanel _controles = new() { Spacing = 6, Margin = new Thickness(16, 10, 16, 8) };
    /// <summary>Cómo saldrá (las hojas, la letra y los cortes): fijo encima de los ajustes, para verlo mientras se cambian.</summary>
    private readonly StackPanel _resumen = new() { Spacing = 2 };
    private readonly ScrollViewer _desplazable;
    private readonly Control _barra, _previa, _pie, _hoja, _botones;
    /// <summary>Si está colocada en dos columnas (pantalla apaisada y ancha) o en una.</summary>
    private bool? _enColumnas;

    internal static PantallaImpresion? Actual { get; private set; }

    public static Task Abrir(Arbol arbol, FormaTarjeta forma, Ordenacion orden, bool marcaHistoria, string nombre) =>
        Principal.Actual.Hoja<bool>(cerrar =>
        {
            var p = new PantallaImpresion(arbol, forma, orden, marcaHistoria, nombre, () => cerrar(false));
            Actual = p;
            return p;
        }, false).ContinueWith(_ => { Actual = null; }, TaskScheduler.FromCurrentSynchronizationContext());

    private PantallaImpresion(Arbol arbol, FormaTarjeta forma, Ordenacion orden, bool marcaHistoria, string nombre, Action cerrar)
    {
        _arbol = arbol; _forma = forma; _orden = orden; _marcaHistoria = marcaHistoria; _nombre = nombre;
        var pref = Preferencias.Actual;
        // la primera vez, el papel habitual en el país del teléfono (Carta en Estados Unidos, A4 en España…)
        _papel = pref.PapelImpresion is { } guardado ? Papel.De(guardado) : Papel.HabitualDe(System.Globalization.CultureInfo.CurrentCulture);
        _hojas = Math.Clamp(pref.HojasImpresion, 1, PlanImpresion.MaxHojas);
        // en color o en blanco y negro (el antiguo «ahorra tinta», en color)
        _estilo = pref.EstiloImpresion == nameof(EstiloPapel.BlancoYNegro) ? EstiloPapel.BlancoYNegro : EstiloPapel.Oscuro;
        _evitarCortes = !pref.CortesIguales;
        _destacar = !pref.SinDestacarImpresion;
        _lamina = pref.Lamina;
        // el estilo y la letra, los de la pantalla; si la última vez se imprimió con ese mismo estilo, con la letra y el marco
        // que se eligieron entonces
        _tema = Dibujo.Tema.Actual;
        bool mismo = pref.TemaImpresion == _tema.Id;
        _letra = (mismo ? Letra.Leer(pref.LetraImpresion) : null) ?? Letra.Actual;
        _estiloLamina = mismo && Enum.TryParse<EstiloLamina>(pref.EstiloLamina, out var el) ? el : _tema.Marco;
        _titulo = pref.ArbolLamina == nombre && pref.TituloLamina != null ? pref.TituloLamina : nombre;
        _dedicatoria = pref.DedicatoriaLamina;
        _opacidad = Math.Clamp(pref.OpacidadFondoLamina, 0.05, 1);
        if (pref.FondoLamina && pref.ArbolLamina == nombre) _foto = CargarFoto();
        _cajaTitulo = Caja(_titulo, T("Familia García López"), t => _titulo = t);
        _cajaDedicatoria = Caja(_dedicatoria, T("Para la abuela, Navidad 2026"), t => _dedicatoria = t);

        // la colocación (en una columna o en dos) la decide Colocar, según el tamaño de la pantalla
        var barra = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Background = Ui.Barra, MinHeight = 56 };
        barra.Children.Add(Ui.BotonIcono(Ui.Iconos.Cerrar, T("Cerrar"), cerrar));
        var titulo = Ui.Titulo(T("Imprimir"), 19); titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 1); barra.Children.Add(titulo);
        Children.Add(_barra = barra);

        // la vista previa, con un aviso de que se puede ampliar y, ampliada, un botón para volver a verla entera
        var previa = new Grid();
        previa.Children.Add(_vista);
        previa.Children.Add(_vistaLamina);
        var pista = new Border
        {
            Child = new TextBlock { Text = App.Escritorio ? T("Rueda del ratón o doble clic para ampliar") : T("Pellizca o toca dos veces para ampliar"), FontSize = Compacta(12), Foreground = Ui.Suave, TextTrimming = TextTrimming.CharacterEllipsis },
            Background = new SolidColorBrush(Dibujo.Estilo.ConAlfa(Dibujo.Tema.Actual.Fondo, 0xC0)), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
        };
        previa.Children.Add(_pista = pista);
        var entera = new Button
        {
            Content = Ui.Icono(Ui.Iconos.Encuadrar, 22), Classes = { "plano" }, Background = Ui.Panel, IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 10, 10),
        };
        entera.Click += (_, _) => VistaActual.Encajar();
        previa.Children.Add(_entera = entera);
        _vista.AmpliacionCambiada += MostrarAmpliacion;
        _vistaLamina.AmpliacionCambiada += MostrarAmpliacion;
        Children.Add(_previa = previa);

        // el resumen va con la vista previa, sobre su mismo fondo: es el pie de lo que se ve, no un ajuste
        var resumen = new Border { Child = _resumen, Padding = new Thickness(16, 2, 16, 10) };
        Children.Add(_pie = resumen);

        // los ajustes, en una hoja aparte (con las esquinas de arriba redondeadas y la barra de desplazamiento siempre a la
        // vista): se ve que es lo que se cambia y que se puede subir y bajar
        _desplazable = new ScrollViewer { Content = _controles, AllowAutoHide = false };
        var hoja = new Border
        {
            Child = _desplazable, Background = Ui.Panel, ClipToBounds = true, BorderBrush = Ui.Linea, Padding = new Thickness(0, 4, 0, 0),
        };
        Children.Add(_hoja = hoja);

        var botones = new UniformGrid { Columns = Ui.LetraGrande ? 1 : 2, Margin = new Thickness(12, 8, 12, 14), Background = Ui.Fondo };
        var compartir = App.Escritorio ? Ui.Boton(T("Guardar PDF"), () => _ = Hacer(imprimir: false), icono: Ui.Iconos.Guardar)
            : Ui.Boton(T("Compartir PDF"), () => _ = Hacer(imprimir: false), icono: Ui.Iconos.Compartir);
        var imprimir = Ui.Boton(T("Imprimir"), () => _ = Hacer(imprimir: true), principal: true, icono: Ui.Iconos.Imprimir);
        foreach (var b in new[] { compartir, imprimir })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch; b.Margin = new Thickness(4); b.FontSize = Media(15);
            botones.Children.Add(b);
        }
        Children.Add(_botones = botones);

        Colocar(false);
        Actualizar();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // apaisada y ancha (una tableta o un teléfono girado): la vista previa a la izquierda y los ajustes a la derecha;
        // si no, uno encima del otro
        Colocar(e.NewSize.Width > e.NewSize.Height && e.NewSize.Width >= 700, e.NewSize.Width);
    }

    private void Colocar(bool enColumnas, double ancho = 0)
    {
        if (enColumnas) ((Border)_hoja).Width = Math.Clamp(ancho * 0.4, 360, 520);
        if (_enColumnas == enColumnas) return;
        _enColumnas = enColumnas;
        var hoja = (Border)_hoja;
        if (enColumnas)
        {
            // la vista previa, con el resumen debajo, ocupa todo el alto; los ajustes, una columna a la derecha
            ColumnDefinitions = new ColumnDefinitions("*,Auto");
            RowDefinitions = new RowDefinitions("Auto,*,Auto");
            Poner(_barra, 0, 0, columnas: 2);
            Poner(_previa, 1, 0);
            Poner(_pie, 2, 0);
            Poner(_hoja, 1, 1);
            Poner(_botones, 2, 1);
            hoja.CornerRadius = new CornerRadius(18, 0, 0, 18);
            hoja.BorderThickness = new Thickness(1, 1, 0, 1);
            hoja.Margin = new Thickness(0, 10, 0, 0);
        }
        else
        {
            // la vista previa y los ajustes se reparten lo que dejan la barra, el resumen y los botones (con la letra grande
            // del sistema, el resumen y los botones ocupan más, y los dos encogen a la vez)
            ColumnDefinitions = new ColumnDefinitions();
            RowDefinitions = new RowDefinitions("Auto,*,Auto,*,Auto");
            Poner(_barra, 0, 0);
            Poner(_previa, 1, 0);
            Poner(_pie, 2, 0);
            Poner(_hoja, 3, 0);
            Poner(_botones, 4, 0);
            hoja.Width = double.NaN;
            hoja.CornerRadius = new CornerRadius(18, 18, 0, 0);
            hoja.BorderThickness = new Thickness(1, 1, 1, 0);
            hoja.Margin = new Thickness(0);
        }
    }

    private static void Poner(Control c, int fila, int columna, int columnas = 1)
    {
        Grid.SetRow(c, fila); Grid.SetColumn(c, columna); Grid.SetColumnSpan(c, columnas);
    }

    /// <summary>
    /// Los colores con los que se imprime: en blanco y negro o en color con su estilo (sin tarjeta y con un estilo oscuro,
    /// sobre papel blanco: los nombres, de colores claros, apenas se leerían).
    /// </summary>
    private EstiloPapel Efectivo => _estilo == EstiloPapel.BlancoYNegro ? _estilo : EnColor;
    private EstiloPapel EnColor => _forma.SinTarjeta && !_tema.Claro ? EstiloPapel.Claro : EstiloPapel.Oscuro;

    /// <summary>El árbol de las hojas, con los colores de ese estilo (se prepara la primera vez que se elige).</summary>
    private LienzoArbol Lienzo => LienzoDe(Efectivo);

    /// <summary>El árbol de la lámina, siempre en color.</summary>
    private LienzoArbol LienzoLamina => LienzoDe(EnColor);

    private LienzoArbol LienzoDe(EstiloPapel estilo)
    {
        var clave = (estilo, _tema, _letra);
        if (_lienzos.TryGetValue(clave, out var l)) { l.Destacar = _destacar; return l; }
        l = new LienzoArbol(estilo, _tema, _letra) { Forma = _forma, Ordenacion = _orden, MarcaHistoria = _marcaHistoria, Destacar = _destacar };
        l.Mostrar(_arbol, false);
        return _lienzos[clave] = l;
    }

    /// <summary>La lámina sale en vertical si así el árbol cabe más grande.</summary>
    private bool LaminaVertical => Lamina.Vertical(LienzoLamina, _papel, _estiloLamina, _titulo, _dedicatoria);

    private PlanImpresion.Configuracion? Plan => PlanImpresion.Mejor(Lienzo.Layout, _papel, _hojas, _evitarCortes);

    private void Guardar()
    {
        var pref = Preferencias.Actual;
        pref.PapelImpresion = _papel.Codigo; pref.HojasImpresion = _hojas; pref.EstiloImpresion = _estilo.ToString();
        pref.CortesIguales = !_evitarCortes;
        pref.SinDestacarImpresion = !_destacar;
        pref.Lamina = _lamina; pref.EstiloLamina = _estiloLamina.ToString(); pref.DedicatoriaLamina = _dedicatoria;
        pref.TemaImpresion = _tema.Id; pref.LetraImpresion = _letra.Id;
        pref.TituloLamina = _titulo; pref.ArbolLamina = _nombre;
        pref.FondoLamina = _foto != null; pref.OpacidadFondoLamina = _opacidad;
        pref.Guardar();
    }

    /// <summary>La vista previa que se ve: la de las hojas o la de la lámina.</summary>
    private Dibujo.VistaAmpliable VistaActual => _lamina ? _vistaLamina : _vista;

    /// <summary>De lejos, el aviso de que se puede ampliar; ampliada, el botón para volver a verla entera.</summary>
    private void MostrarAmpliacion()
    {
        var v = VistaActual;
        _pista.IsVisible = !v.Ampliada; _entera.IsVisible = v.Ampliada;
        ToolTip.SetTip(_entera, _lamina ? T("Ver la lámina entera") : T("Ver todas las hojas"));
    }

    /// <summary>Rehace los ajustes (con lo elegido marcado), lo que se dice de la impresión y la vista previa.</summary>
    private void Actualizar()
    {
        var donde = _desplazable.Offset;
        _controles.Children.Clear();
        _resumen.Children.Clear();
        _vista.IsVisible = !_lamina; _vistaLamina.IsVisible = _lamina;
        MostrarAmpliacion();

        // lo primero, qué se hace: las hojas para pegar o la lámina para regalar
        _controles.Children.Add(Segmentos(new[] { T("Hojas para pegar"), T("Lámina para regalar") }, _lamina ? 1 : 0,
            i => { _lamina = i == 1; Cambiado(); }));
        if (_lamina) { ActualizarLamina(); Recolocar(donde); return; }

        var plan = Plan;
        _vista.Marca = Licencia.ConMarca;
        _vista.Mostrar(Lienzo, plan);

        // cómo saldrá: las hojas, la letra (con su color: rojo si no se leerá) y si se corta alguna tarjeta
        if (plan != null)
        {
            // en dos líneas y con una letra que crece menos que la del sistema: con la letra muy grande, el resumen no
            // puede comerse el sitio de los ajustes
            _resumen.Children.Add(new TextBlock { Text = Mayuscula(plan.Reparto), FontSize = Compacta(15), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var lectura = PlanImpresion.Configuracion.Legibilidad(plan.Puntos(Lienzo.TamLetraNombre));
            IBrush color = lectura switch
            {
                PlanImpresion.Lectura.MuyPequena => Ui.Rojo, PlanImpresion.Lectura.Pequena => Ui.Oro, _ => Ui.Verde,
            };
            // la letra, de su color, y detrás (en gris) si los cortes parten tarjetas
            var letra = new TextBlock { FontSize = Compacta(13.5), TextWrapping = TextWrapping.Wrap };
            letra.Inlines!.Add(new Avalonia.Controls.Documents.Run(Mayuscula(plan.Letra(Lienzo.TamLetraNombre))) { Foreground = color });
            if (plan.Cortes.Length > 0)
                letra.Inlines.Add(new Avalonia.Controls.Documents.Run("  ·  " + plan.Cortes) { Foreground = Ui.Suave });
            _resumen.Children.Add(letra);
            _controles.Children.Add(Parrafo(lectura <= PlanImpresion.Lectura.Pequena
                ? T("Un libro usa letra de 10 a 12 pt. Para que salga más grande: más hojas, un papel mayor o tarjetas sin foto.")
                : T("Un libro usa letra de 10 a 12 pt."), 13));
        }

        ElegirPapel();

        Titulo(T("Hojas"));
        _controles.Children.Add(Segmentos(Enumerable.Range(1, PlanImpresion.MaxHojas).Select(n => n.ToString(Cultura)).ToList(), _hojas - 1,
            i => { _hojas = i + 1; Cambiado(); }, columnas: 5));
        if (_hojas > 1)
        {
            var casilla = new CheckBox { Content = T("No cortar tarjetas"), FontSize = Media(15), IsChecked = _evitarCortes, Margin = new Thickness(0, 2, 0, 0) };
            casilla.IsCheckedChanged += (_, _) => { _evitarCortes = casilla.IsChecked == true; Cambiado(); };
            _controles.Children.Add(casilla);
            _controles.Children.Add(Parrafo(T("Cada hoja deja un margen de 12 mm. Para juntarlas, se recorta cada hoja por su línea de corte y se pega sobre el margen gris de la anterior: el dibujo sigue sin cortes."), 13));
        }

        ElegirDestacar();

        ElegirEstilo();
        Titulo(T("Cómo sale"));
        var estilos = new[] { EstiloPapel.Oscuro, EstiloPapel.BlancoYNegro };
        _controles.Children.Add(Segmentos(new[] { T("En color"), T("Blanco y negro") }, Array.IndexOf(estilos, _estilo),
            i => { _estilo = estilos[i]; Cambiado(); }));
        _controles.Children.Add(Parrafo(Efectivo switch
        {
            EstiloPapel.Oscuro => _tema.Claro ? T("Con los colores y el dibujo del estilo, como en la pantalla.")
                : T("Con los colores y el dibujo del estilo, como en la pantalla. Este es oscuro: gasta mucha tinta."),
            EstiloPapel.BlancoYNegro => T("Para impresoras con solo tinta negra: todo en negro y grises bien distintos."),
            _ => T("Los nombres en su color, sobre el papel blanco. Sin tarjeta no hay oscuro: apenas se leerían."),
        }, 13));

        Recolocar(donde);
    }

    /// <summary>Deja los ajustes donde estaban (al rehacerlos, el desplazamiento volvería arriba).</summary>
    private void Recolocar(Vector donde)
    {
        _desplazable.Offset = donde;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _desplazable.Offset = donde, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Los ajustes de la lámina: el título, la dedicatoria, el estilo y el papel.</summary>
    private void ActualizarLamina()
    {
        MostrarLamina();
        if (!Licencia.Completa)
            _controles.Children.Add(Parrafo(T("Las láminas para regalar son de Ramo completo. Puedes probarlas aquí todo lo que quieras."), 13));

        Titulo(T("Título"));
        _controles.Children.Add(_cajaTitulo);
        Titulo(T("Dedicatoria (si quieres)"));
        _controles.Children.Add(_cajaDedicatoria);

        ElegirEstilo();
        Titulo(T("Marco"));
        var marcos = Lamina.Estilos;
        _controles.Children.Add(Segmentos(marcos.Select(Lamina.Nombre).ToList(), marcos.ToList().IndexOf(_estiloLamina),
            i => { _estiloLamina = marcos[i]; Cambiado(); }, columnas: 3));
        ElegirFondo();
        ElegirDestacar();

        ElegirPapel();
        _controles.Children.Add(Parrafo(App.Escritorio
            ? T("Una sola hoja, lista para enmarcar. En A3 el árbol sale el doble de grande; si no tienes impresora de A3, guarda el PDF y llévalo a una imprenta o copistería.")
            : T("Una sola hoja, lista para enmarcar. En A3 el árbol sale el doble de grande; si no tienes impresora de A3, comparte el PDF y llévalo a una imprenta o copistería."), 13));
    }

    /// <summary>Dónde se guarda la foto de fondo de la lámina (una copia ya reducida).</summary>
    private static string RutaFoto => Path.Combine(App.Plataforma.CarpetaDatos, "fondo-lamina.jpg");

    /// <summary>La foto de fondo guardada (leída entera, para poder cambiarla luego sin que el archivo esté en uso).</summary>
    private static Avalonia.Media.Imaging.Bitmap? CargarFoto()
    {
        try { return File.Exists(RutaFoto) ? new Avalonia.Media.Imaging.Bitmap(new MemoryStream(File.ReadAllBytes(RutaFoto))) : null; }
        catch { return null; }
    }

    /// <summary>La foto de fondo de la lámina, si la hay, con su intensidad.</summary>
    private FondoLamina? Fondo => _foto is { } f ? new FondoLamina(f, _opacidad) : null;

    /// <summary>Poner, cambiar o quitar la foto de fondo de la lámina y, con foto, cómo de intensa sale.</summary>
    private void ElegirFondo()
    {
        Titulo(T("Foto de fondo"));
        if (_foto == null)
        {
            var poner = Ui.Boton(T("Poner una foto"), () => _ = PonerFoto(), icono: Ui.Iconos.Galeria);
            poner.HorizontalAlignment = HorizontalAlignment.Stretch; poner.FontSize = Media(15); poner.Margin = new Thickness(0, 2);
            _controles.Children.Add(poner);
            return;
        }
        var botones = new UniformGrid { Columns = 2 };
        foreach (var b in new[]
        {
            Ui.Boton(T("Cambiar la foto"), () => _ = PonerFoto(), icono: Ui.Iconos.Galeria),
            Ui.Boton(T("Quitar la foto"), () => { _foto = null; Cambiado(); }),
        })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch; b.FontSize = Media(15); b.Margin = new Thickness(0, 2, 4, 2);
            botones.Children.Add(b);
        }
        _controles.Children.Add(botones);

        Titulo(T("Intensidad de la foto"));
        var fila = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var cuanto = new TextBlock { FontSize = Media(15), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, MinWidth = Media(48), TextAlignment = TextAlignment.Right };
        void Rotulo() => cuanto.Text = $"{Math.Round(_opacidad * 100).ToString(Cultura)} %";
        Rotulo();
        var barra = new Slider
        {
            Minimum = 5, Maximum = 100, Value = Math.Round(_opacidad * 100), SmallChange = 5, LargeChange = 10,
            TickFrequency = 5, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center,
        };
        // al moverla solo se rehace la vista previa (rehacer los ajustes soltaría la barra a medio arrastrar)
        barra.ValueChanged += (_, e) => { _opacidad = e.NewValue / 100; Rotulo(); Guardar(); MostrarLamina(); };
        fila.Children.Add(barra);
        Grid.SetColumn(cuanto, 1); fila.Children.Add(cuanto);
        _controles.Children.Add(fila);
        _controles.Children.Add(Parrafo(T("La foto va dentro del marco, debajo del árbol. Si cuesta leer el título o los nombres, baja la intensidad."), 13));
    }

    /// <summary>Elige una foto de la galería (o del ordenador) para el fondo de la lámina.</summary>
    private async Task PonerFoto()
    {
        if (await EditorPersona.ElegirImagen(this) is not { } datos) return;
        if (!Fotos.GuardarFondo(datos, RutaFoto) || CargarFoto() is not { } foto) { Principal.Actual.Aviso(T("No se pudo leer esa imagen")); return; }
        _foto = foto;
        Cambiado();
    }

    /// <summary>
    /// Si se destaca a la persona principal (su línea directa en dorado y la familia política con el borde discontinuo) o
    /// sale un árbol de toda la familia, sin nadie destacado. Sin persona principal no hay nada que destacar.
    /// </summary>
    private void ElegirDestacar()
    {
        if (_arbol.Buscar(_arbol.RaizId) is not { } raiz) return;
        Titulo(T("Persona principal"));
        var casilla = new CheckBox
        {
            Content = new TextBlock { Text = T("Destacar a {0} y su línea directa", Nombres.Mostrar(raiz)), TextWrapping = TextWrapping.Wrap },
            FontSize = Media(15), IsChecked = _destacar, Margin = new Thickness(0, 2, 0, 0),
        };
        casilla.IsCheckedChanged += (_, _) => { _destacar = casilla.IsChecked == true; Cambiado(); };
        _controles.Children.Add(casilla);
        _controles.Children.Add(Parrafo(_destacar
            ? T("Su línea directa sale en dorado y la familia política, con el borde discontinuo.")
            : T("Un árbol de toda la familia: todas las tarjetas y las líneas son iguales."), 13));
    }

    /// <summary>
    /// El estilo del árbol (los mismos que en la pantalla) y la letra de las tarjetas, en las hojas y en la lámina. Al elegir
    /// un estilo se ponen su letra y su marco; luego se pueden cambiar.
    /// </summary>
    private void ElegirEstilo()
    {
        Titulo(T("Estilo"));
        var temas = Dibujo.Tema.Todos;
        _controles.Children.Add(Segmentos(temas.Select(t => t.Nombre).ToList(), temas.ToList().IndexOf(_tema), i =>
        {
            _tema = temas[i]; _letra = Letra.DelTema(_tema); _estiloLamina = _tema.Marco;
            Cambiado();
        }, columnas: 2));
        Titulo(T("Letra"));
        var letras = Letra.Todas;
        _controles.Children.Add(Segmentos(letras.Select(l => l.Nombre).ToList(), letras.ToList().IndexOf(_letra),
            i => { _letra = letras[i]; Cambiado(); }, columnas: 3));
    }

    /// <summary>Los papeles para elegir, los mismos en las hojas para pegar y en la lámina: primero los del país del teléfono.</summary>
    private void ElegirPapel()
    {
        var habitual = Papel.HabitualDe(System.Globalization.CultureInfo.CurrentCulture);
        var iso = Papel.Todos.Where(p => !p.Americano).ToList();
        var americanos = Papel.Todos.Where(p => p.Americano).ToList();
        var filas = habitual.Americano ? new[] { americanos, iso } : new[] { iso, americanos };
        Titulo(T("Papel"));
        foreach (var fila in filas)
            _controles.Children.Add(Segmentos(fila.Select(p => p.Nombre).ToList(), fila.IndexOf(_papel), i => { _papel = fila[i]; Cambiado(); }));
        _controles.Children.Add(Parrafo($"{_papel.Nombre}: {_papel.Medidas}", 13));
    }

    /// <summary>La vista previa de la lámina y, debajo, cómo saldrá (el papel y el tamaño de la letra de los nombres).</summary>
    private void MostrarLamina()
    {
        bool vertical = LaminaVertical;
        var pagina = Lamina.Pagina(_papel, vertical);
        _vistaLamina.Mostrar(LienzoLamina, pagina, _estiloLamina, _titulo, _dedicatoria, Fondo);
        _resumen.Children.Clear();
        _resumen.Children.Add(new TextBlock
        {
            Text = Mayuscula(vertical ? T("1 hoja {0} vertical", _papel.Nombre) : T("1 hoja {0} horizontal", _papel.Nombre)),
            FontSize = Compacta(15), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
        });
        double pt = LienzoLamina.TamLetraNombre * Lamina.EscalaArbol(LienzoLamina, pagina, _estiloLamina, _titulo, _dedicatoria);
        var lectura = PlanImpresion.Configuracion.Legibilidad(pt);
        _resumen.Children.Add(new TextBlock
        {
            Text = Mayuscula(T("nombres en letra de {0} pt ({1})", pt.ToString(pt < 10 ? "0.0" : "0", Cultura), PlanImpresion.Configuracion.Texto(lectura))),
            FontSize = Compacta(13.5), TextWrapping = TextWrapping.Wrap,
            Foreground = lectura switch { PlanImpresion.Lectura.MuyPequena => Ui.Rojo, PlanImpresion.Lectura.Pequena => Ui.Oro, _ => Ui.Verde },
        });
    }

    /// <summary>Un cuadro de texto de una línea; al escribir se ve en la lámina (sin rehacer los ajustes, para no perder el cursor).</summary>
    private TextBox Caja(string texto, string ejemplo, Action<string> cambiar)
    {
        var caja = new TextBox { Text = texto, PlaceholderText = ejemplo, FontSize = Media(16), MaxLength = 80, Margin = new Thickness(0, 2) };
        caja.TextChanged += (_, _) =>
        {
            cambiar(caja.Text ?? "");
            Guardar();
            if (_lamina) MostrarLamina();
        };
        return caja;
    }

    private void Cambiado()
    {
        Guardar();
        Actualizar();
    }

    private void Titulo(string texto) =>
        _controles.Children.Add(new TextBlock { Text = texto, FontSize = Media(13), Foreground = Ui.Suave, Margin = new Thickness(2, 10, 0, 0) });

    /// <summary>Un tamaño de letra que sigue al del sistema, pero solo hasta un 20 % más grande.</summary>
    private static double Compacta(double tam) => Math.Round(tam * Math.Min(Ui.Escala, 1.2), 1);

    /// <summary>
    /// La letra de los ajustes y de los botones: sigue a la del sistema hasta 1,5 veces la normal (más grande, los botones
    /// de papel no cabrían y apenas se verían opciones).
    /// </summary>
    private static double Media(double tam) => Math.Round(tam * Math.Min(Ui.Escala, 1.5), 1);

    private static TextBlock Parrafo(string s, double tam) =>
        new() { Text = s, FontSize = Media(tam), Foreground = Ui.Suave, TextWrapping = TextWrapping.Wrap, LineHeight = Media(tam) * 1.4 };

    private static string Mayuscula(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Cultura) + s[1..];

    /// <summary>Unos botones juntos en una pastilla, de los que se elige uno (el elegido, en dorado).</summary>
    private static Control Segmentos(IReadOnlyList<string> textos, int elegido, Action<int> elegir, int? columnas = null, bool[]? activos = null)
    {
        var rejilla = new UniformGrid { Columns = columnas ?? textos.Count };
        for (int i = 0; i < textos.Count; i++)
        {
            int k = i;
            bool marcado = i == elegido;
            var b = new Button
            {
                Content = new TextBlock
                {
                    Text = textos[i], TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = Media(15),
                    Foreground = marcado ? Ui.SobreOro : Ui.Texto,
                    FontWeight = marcado ? FontWeight.SemiBold : FontWeight.Normal,
                },
                Classes = { "segmento" }, Background = marcado ? Ui.Oro : Brushes.Transparent,
                IsEnabled = activos?[i] ?? true,
            };
            b.Click += (_, _) => { if (k != elegido) elegir(k); };
            rejilla.Children.Add(b);
        }
        return new Border
        {
            Child = rejilla, Background = Ui.Fondo, CornerRadius = new CornerRadius(12), BorderBrush = Ui.Linea, BorderThickness = new Thickness(1),
            Padding = new Thickness(2), Margin = new Thickness(0, 2),
        };
    }

    /// <summary>Prepara el PDF de las hojas y lo manda a imprimir o lo comparte.</summary>
    private async Task Hacer(bool imprimir)
    {
        if (_lamina) { await HacerLamina(imprimir); return; }
        if (Plan is not { } plan) return;
        var p = Principal.Actual;
        try
        {
            var destino = App.Biblioteca.ArchivoParaCompartir(T("{0} (para imprimir)", _nombre), ".pdf");
            p.Aviso(T("Preparando las hojas…"));
            await Task.Yield();
            await HojasImpresion.GuardarPdf(Lienzo, plan, destino, _nombre, Licencia.ConMarca);
            if (imprimir) await App.Plataforma.Imprimir(destino, _nombre, plan.Papel, plan.Vertical, plan.Hojas);
            else if (App.Escritorio) await Ordenador.Exportar.GuardarCopia(destino, T("{0} (para imprimir)", _nombre), "PDF");
            else await App.Plataforma.Compartir(new[] { destino }, "application/pdf", T("Compartir las hojas de «{0}»", _nombre));
        }
        catch (Exception ex) { await p.Error(T("No se pudo completar"), ex); }
    }

    /// <summary>Prepara el PDF de la lámina (con Ramo completo) y lo manda a imprimir o lo comparte.</summary>
    private async Task HacerLamina(bool imprimir)
    {
        if (!Licencia.Completa && !await PantallaCompleta.Ofrecer(T("Las láminas para regalar, con título, dedicatoria y marco, son de Ramo completo.")))
            return;
        var p = Principal.Actual;
        try
        {
            var destino = App.Biblioteca.ArchivoParaCompartir(T("{0} (lámina)", _nombre), ".pdf");
            p.Aviso(T("Preparando la lámina…"));
            await Task.Yield();
            bool vertical = LaminaVertical;
            await Lamina.GuardarPdf(destino, LienzoLamina, _papel, vertical, _estiloLamina, _titulo, _dedicatoria, Fondo);
            if (imprimir) await App.Plataforma.Imprimir(destino, _nombre, _papel, vertical, 1);
            else if (App.Escritorio) await Ordenador.Exportar.GuardarCopia(destino, T("{0} (lámina)", _nombre), "PDF");
            else await App.Plataforma.Compartir(new[] { destino }, "application/pdf", T("Compartir la lámina de «{0}»", _nombre));
        }
        catch (Exception ex) { await p.Error(T("No se pudo completar"), ex); }
    }

    // ---------- Pruebas (la ventana de escritorio, con --captura) ----------

    /// <summary>
    /// «papel=Letter», «hojas=3», «estilo=BlancoYNegro», «cortes=iguales», «destacar=no», «pdf=ruta», «ampliar=3» o «ampliar=0.3,0.5» y
    /// «mover=dx,dy»; el estilo del árbol: «estilo-arbol=pergamino» (con su letra y su marco) y «letra=lora»; la lámina:
    /// «lamina=Floral» (el marco; o «lamina=no»), «titulo=…», «dedicatoria=…», «fondo=ruta» (o «fondo=no»),
    /// «opacidad=0.4», «lamina-pdf=ruta» y
    /// «lamina-png=ruta» (a 2 píxeles por punto).
    /// </summary>
    internal async Task Prueba(string clave, string valor)
    {
        switch (clave)
        {
            case "papel": _papel = Papel.De(valor); break;
            case "hojas": _hojas = int.Parse(valor); break;
            case "estilo": _estilo = Enum.Parse<EstiloPapel>(valor); break;
            case "cortes": _evitarCortes = valor != "iguales"; break;
            case "destacar": _destacar = valor != "no"; break;
            case "lamina":
                _lamina = valor != "no";
                if (_lamina && valor.Length > 0) _estiloLamina = Enum.Parse<EstiloLamina>(valor);
                break;
            case "titulo": _cajaTitulo.Text = valor; return;
            case "dedicatoria": _cajaDedicatoria.Text = valor; return;
            case "lamina-pdf": await Lamina.GuardarPdf(valor, LienzoLamina, _papel, LaminaVertical, _estiloLamina, _titulo, _dedicatoria, Fondo); return;
            case "fondo":
                if (valor == "no") _foto = null;
                else if (Fotos.GuardarFondo(File.ReadAllBytes(valor), RutaFoto)) _foto = CargarFoto();
                break;
            // el estilo del árbol (con su letra y su marco) y la letra
            case "estilo-arbol": _tema = Dibujo.Tema.Leer(valor); _letra = Letra.DelTema(_tema); _estiloLamina = _tema.Marco; break;
            case "letra": _letra = Letra.Leer(valor) ?? _letra; break;
            case "opacidad": _opacidad = double.Parse(valor, System.Globalization.CultureInfo.InvariantCulture); break;
            case "lamina-png":
                using (var img = Lamina.Imagen(LienzoLamina, _papel, LaminaVertical, _estiloLamina, _titulo, _dedicatoria, 2, Fondo)) img.Save(valor);
                return;
            case "pdf": if (Plan is { } plan) await HojasImpresion.GuardarPdf(Lienzo, plan, valor, _nombre, Licencia.ConMarca); return;
            // amplía la vista previa en su centro, o con «x,y» (proporción del ancho y del alto) en ese punto, como el doble toque
            case "ampliar":
                if (valor.Contains(','))
                {
                    var xy = valor.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    VistaActual.AlternarAmpliacion(new Point(VistaActual.Bounds.Width * xy[0], VistaActual.Bounds.Height * xy[1]));
                }
                else VistaActual.Ampliar(double.Parse(valor, System.Globalization.CultureInfo.InvariantCulture), VistaActual.Bounds.Center);
                return;
            case "mover": { var d = valor.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); VistaActual.Mover(d[0], d[1]); return; }
        }
        Actualizar();
    }
}
