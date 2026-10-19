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
/// </summary>
public sealed class PantallaImpresion : Grid
{
    private readonly Arbol _arbol;
    private readonly FormaTarjeta _forma;
    private readonly Ordenacion _orden;
    private readonly bool _marcaHistoria;
    private readonly string _nombre;
    private readonly Dictionary<EstiloPapel, LienzoArbol> _lienzos = new();

    private Papel _papel;
    private int _hojas;
    private EstiloPapel _estilo;
    private bool _evitarCortes;

    private readonly VistaHojas _vista = new();
    private readonly StackPanel _controles = new() { Spacing = 6, Margin = new Thickness(16, 10, 16, 8) };
    /// <summary>Cómo saldrá (las hojas, la letra y los cortes): fijo encima de los ajustes, para verlo mientras se cambian.</summary>
    private readonly StackPanel _resumen = new() { Spacing = 2 };
    private readonly ScrollViewer _desplazable;

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
        _estilo = Enum.TryParse<EstiloPapel>(pref.EstiloImpresion, out var e) ? e : EstiloPapel.Claro;
        _evitarCortes = !pref.CortesIguales;

        // la vista previa y los ajustes se reparten lo que dejan la barra, el resumen y los botones (con la letra grande del
        // sistema, el resumen y los botones ocupan más, y los dos encogen a la vez)
        RowDefinitions = new RowDefinitions("Auto,5*,Auto,4*,Auto");
        var barra = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Background = Ui.Barra, MinHeight = 56 };
        barra.Children.Add(Ui.BotonIcono(Ui.Iconos.Cerrar, T("Cerrar"), cerrar));
        var titulo = Ui.Titulo(T("Imprimir"), 19); titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 1); barra.Children.Add(titulo);
        Children.Add(barra);

        // la vista previa, con un aviso de que se puede ampliar y, ampliada, un botón para volver a verla entera
        var previa = new Grid();
        previa.Children.Add(_vista);
        var pista = new Border
        {
            Child = new TextBlock { Text = T("Pellizca o toca dos veces para ampliar"), FontSize = Ui.L(12), Foreground = Ui.Suave },
            Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x0E, 0x10, 0x16)), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
        };
        previa.Children.Add(pista);
        var entera = new Button
        {
            Content = Ui.Icono(Ui.Iconos.Encuadrar, 22), Classes = { "plano" }, Background = Ui.Panel, IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 10, 10),
        };
        ToolTip.SetTip(entera, T("Ver todas las hojas"));
        entera.Click += (_, _) => _vista.Encajar();
        previa.Children.Add(entera);
        _vista.AmpliacionCambiada += () => { entera.IsVisible = _vista.Ampliada; pista.IsVisible = !_vista.Ampliada; };
        Grid.SetRow(previa, 1);
        Children.Add(previa);

        var resumen = new Border
        {
            Child = _resumen, Background = Ui.Panel, Padding = new Thickness(16, 10, 16, 8),
            BorderBrush = Ui.Linea, BorderThickness = new Thickness(0, 0, 0, 1),
        };
        Grid.SetRow(resumen, 2);
        Children.Add(resumen);

        _desplazable = new ScrollViewer { Content = _controles, Background = Ui.Panel };
        Grid.SetRow(_desplazable, 3);
        Children.Add(_desplazable);

        var botones = new UniformGrid { Columns = Ui.LetraGrande ? 1 : 2, Margin = new Thickness(12, 8, 12, 14), Background = Ui.Fondo };
        var compartir = Ui.Boton(T("Compartir PDF"), () => _ = Hacer(imprimir: false), icono: Ui.Iconos.Compartir);
        var imprimir = Ui.Boton(T("Imprimir"), () => _ = Hacer(imprimir: true), principal: true, icono: Ui.Iconos.Imprimir);
        foreach (var b in new[] { compartir, imprimir })
        {
            b.HorizontalAlignment = HorizontalAlignment.Stretch; b.Margin = new Thickness(4);
            botones.Children.Add(b);
        }
        Grid.SetRow(botones, 4);
        Children.Add(botones);

        Actualizar();
    }

    /// <summary>El estilo con el que se imprime: sin tarjeta no hay oscuro (los nombres, de colores claros, apenas se leerían).</summary>
    private EstiloPapel Efectivo => _forma.SinTarjeta && _estilo == EstiloPapel.Oscuro ? EstiloPapel.Claro : _estilo;

    /// <summary>El árbol dibujado con los colores de ese estilo (se prepara la primera vez que se elige).</summary>
    private LienzoArbol Lienzo
    {
        get
        {
            if (_lienzos.TryGetValue(Efectivo, out var l)) return l;
            l = new LienzoArbol(Efectivo) { Forma = _forma, Ordenacion = _orden, MarcaHistoria = _marcaHistoria };
            l.Mostrar(_arbol, false);
            return _lienzos[Efectivo] = l;
        }
    }

    private PlanImpresion.Configuracion? Plan => PlanImpresion.Mejor(Lienzo.Layout, _papel, _hojas, _evitarCortes);

    private void Guardar()
    {
        var pref = Preferencias.Actual;
        pref.PapelImpresion = _papel.Codigo; pref.HojasImpresion = _hojas; pref.EstiloImpresion = _estilo.ToString();
        pref.CortesIguales = !_evitarCortes;
        pref.Guardar();
    }

    /// <summary>Rehace los ajustes (con lo elegido marcado), lo que se dice de la impresión y la vista previa.</summary>
    private void Actualizar()
    {
        var plan = Plan;
        _vista.Mostrar(Lienzo, plan);
        var donde = _desplazable.Offset;
        _controles.Children.Clear();
        _resumen.Children.Clear();

        // cómo saldrá: las hojas, la letra (con su color: rojo si no se leerá) y si se corta alguna tarjeta
        if (plan != null)
        {
            _resumen.Children.Add(new TextBlock { Text = Mayuscula(plan.Reparto), FontSize = Ui.L(16), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var lectura = PlanImpresion.Configuracion.Legibilidad(plan.Puntos(Lienzo.TamLetraNombre));
            IBrush color = lectura switch
            {
                PlanImpresion.Lectura.MuyPequena => Ui.Rojo, PlanImpresion.Lectura.Pequena => Ui.Oro, _ => Ui.Verde,
            };
            _resumen.Children.Add(new TextBlock { Text = Mayuscula(plan.Letra(Lienzo.TamLetraNombre)), FontSize = Ui.L(15), Foreground = color, TextWrapping = TextWrapping.Wrap });
            if (plan.Cortes.Length > 0)
                _resumen.Children.Add(new TextBlock { Text = Mayuscula(plan.Cortes), FontSize = Ui.L(14), Foreground = Ui.Suave, TextWrapping = TextWrapping.Wrap });
            _controles.Children.Add(Ui.Parrafo(lectura <= PlanImpresion.Lectura.Pequena
                ? T("Un libro usa letra de 10 a 12 pt. Para que salga más grande: más hojas, un papel mayor o tarjetas sin foto.")
                : T("Un libro usa letra de 10 a 12 pt."), 13));
        }

        // el papel: primero los del país del teléfono
        var habitual = Papel.HabitualDe(System.Globalization.CultureInfo.CurrentCulture);
        var iso = Papel.Todos.Where(p => !p.Americano).ToList();
        var americanos = Papel.Todos.Where(p => p.Americano).ToList();
        var filas = habitual.Americano ? new[] { americanos, iso } : new[] { iso, americanos };
        Titulo(T("Papel"));
        foreach (var fila in filas)
            _controles.Children.Add(Segmentos(fila.Select(p => p.Nombre).ToList(), fila.IndexOf(_papel), i => { _papel = fila[i]; Cambiado(); }));
        _controles.Children.Add(Ui.Parrafo($"{_papel.Nombre}: {_papel.Medidas}", 13));

        Titulo(T("Hojas"));
        _controles.Children.Add(Segmentos(Enumerable.Range(1, PlanImpresion.MaxHojas).Select(n => n.ToString(Cultura)).ToList(), _hojas - 1,
            i => { _hojas = i + 1; Cambiado(); }, columnas: 5));
        if (_hojas > 1)
        {
            var casilla = new CheckBox { Content = T("No cortar tarjetas"), IsChecked = _evitarCortes, Margin = new Thickness(0, 2, 0, 0) };
            casilla.IsCheckedChanged += (_, _) => { _evitarCortes = casilla.IsChecked == true; Cambiado(); };
            _controles.Children.Add(casilla);
            _controles.Children.Add(Ui.Parrafo(T("Cada hoja deja un margen de 12 mm. Para juntarlas, se recorta cada hoja por su línea de corte y se pega sobre el margen gris de la anterior: el dibujo sigue sin cortes."), 13));
        }

        Titulo(T("Colores"));
        var estilos = new[] { EstiloPapel.Claro, EstiloPapel.Oscuro, EstiloPapel.BlancoYNegro };
        _controles.Children.Add(Segmentos(new[] { T("Claro"), T("Oscuro"), T("Blanco y negro") }, Array.IndexOf(estilos, Efectivo),
            i => { _estilo = estilos[i]; Cambiado(); }, activos: new[] { true, !_forma.SinTarjeta, true }));
        _controles.Children.Add(Ui.Parrafo(Efectivo switch
        {
            EstiloPapel.Oscuro => T("Como en la pantalla: tarjetas de color oscuro. Gasta mucha tinta."),
            EstiloPapel.BlancoYNegro => T("Para impresoras con solo tinta negra: todo en negro y grises bien distintos."),
            _ => _forma.SinTarjeta
                ? T("Los nombres en su color, sobre el papel blanco. Sin tarjeta no hay oscuro: apenas se leerían.")
                : T("Tarjetas blancas con el borde de su color: gasta poca tinta."),
        }, 13));

        _desplazable.Offset = donde;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _desplazable.Offset = donde, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void Cambiado()
    {
        Guardar();
        Actualizar();
    }

    private void Titulo(string texto) =>
        _controles.Children.Add(new TextBlock { Text = texto, FontSize = Ui.L(13), Foreground = Ui.Suave, Margin = new Thickness(2, 10, 0, 0) });

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
                    Text = textos[i], TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = Ui.L(15),
                    Foreground = marcado ? new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x05)) : Ui.Texto,
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
        if (Plan is not { } plan) return;
        var p = Principal.Actual;
        try
        {
            var destino = App.Biblioteca.ArchivoParaCompartir(T("{0} (para imprimir)", _nombre), ".pdf");
            p.Aviso(T("Preparando las hojas…"));
            await Task.Yield();
            await HojasImpresion.GuardarPdf(Lienzo, plan, destino, _nombre);
            if (imprimir) await App.Plataforma.Imprimir(destino, _nombre, plan.Papel, plan.Vertical, plan.Hojas);
            else await App.Plataforma.Compartir(new[] { destino }, "application/pdf", T("Compartir las hojas de «{0}»", _nombre));
        }
        catch (Exception ex) { await p.Error(T("No se pudo completar"), ex); }
    }

    // ---------- Pruebas (la ventana de escritorio, con --captura) ----------

    /// <summary>«papel=Letter», «hojas=3», «estilo=BlancoYNegro», «cortes=iguales», «pdf=ruta», «ampliar=3» o «ampliar=0.3,0.5» y «mover=dx,dy».</summary>
    internal async Task Prueba(string clave, string valor)
    {
        switch (clave)
        {
            case "papel": _papel = Papel.De(valor); break;
            case "hojas": _hojas = int.Parse(valor); break;
            case "estilo": _estilo = Enum.Parse<EstiloPapel>(valor); break;
            case "cortes": _evitarCortes = valor != "iguales"; break;
            case "pdf": if (Plan is { } plan) await HojasImpresion.GuardarPdf(Lienzo, plan, valor, _nombre); return;
            // amplía la vista previa en su centro, o con «x,y» (proporción del ancho y del alto) en ese punto, como el doble toque
            case "ampliar":
                if (valor.Contains(','))
                {
                    var xy = valor.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    _vista.AlternarAmpliacion(new Point(_vista.Bounds.Width * xy[0], _vista.Bounds.Height * xy[1]));
                }
                else _vista.Ampliar(double.Parse(valor, System.Globalization.CultureInfo.InvariantCulture), _vista.Bounds.Center);
                return;
            case "mover": { var d = valor.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); _vista.Mover(d[0], d[1]); return; }
        }
        Actualizar();
    }
}
