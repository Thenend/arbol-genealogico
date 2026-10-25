using Avalonia;
using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>Piezas pequeñas para construir las pantallas: iconos de trazo (24 × 24), botones y textos.</summary>
public static class Ui
{
    /// <summary>
    /// La escala del tamaño de letra del sistema (en Android, Ajustes → Pantalla → Tamaño de fuente; 1 = normal, hasta 2).
    /// Los textos de la aplicación la siguen; las tarjetas del árbol no, porque el árbol ya se amplía con los dedos.
    /// </summary>
    public static double Escala { get; set; } = 1;

    /// <summary>Un tamaño de letra (el de la letra normal) en la escala del sistema.</summary>
    public static double L(double tam) => Math.Round(tam * Escala, 1);

    /// <summary>
    /// Los iconos crecen con la letra, pero menos (si crecieran igual, con la letra al doble ocuparían media barra).
    /// </summary>
    public static double EscalaIcono => 1 + (Escala - 1) * 0.6;

    /// <summary>Con la letra muy grande, lo que va en fila se pone en columna para que quepa.</summary>
    public static bool LetraGrande => Escala > 1.25;

    // Los colores de las pantallas, los del tema (ver Dibujo.Tema). Son siempre los mismos pinceles: al cambiar de tema se
    // les cambia el color, y todo lo que los usa se vuelve a pintar solo.
    public static readonly SolidColorBrush Fondo = new(), Barra = new(), Panel = new(), Linea = new(), Texto = new(), Suave = new();
    /// <summary>Lo elegido en los menús (dorado en Noche) y el texto que va encima cuando es fondo.</summary>
    public static readonly SolidColorBrush Oro = new(), SobreOro = new();
    public static readonly SolidColorBrush Rojo = new(), Verde = new();
    /// <summary>El botón principal (dorado en Noche) y su texto; el fondo de los secundarios; el de los avisos de abajo.</summary>
    public static readonly SolidColorBrush BotonFondo = new(), BotonEncima = new(), BotonPulsado = new(), BotonTexto = new(), Secundario = new(), Aviso = new();

    static Ui() => PonerColores(Dibujo.Tema.Actual);

    /// <summary>Pone los colores de ese tema en los pinceles (y en los recursos que usan los estilos de App.axaml).</summary>
    public static void PonerColores(Dibujo.Tema t)
    {
        Fondo.Color = t.Fondo; Barra.Color = t.Barra; Panel.Color = t.Panel; Linea.Color = t.Linea; Texto.Color = t.Texto; Suave.Color = t.Suave;
        Oro.Color = t.Destacado; SobreOro.Color = t.SobreDestacado; Rojo.Color = t.Rojo; Verde.Color = t.Verde;
        BotonFondo.Color = t.Boton; BotonEncima.Color = t.BotonEncima; BotonPulsado.Color = t.BotonPulsado; BotonTexto.Color = t.BotonTexto;
        Secundario.Color = t.Secundario; Aviso.Color = t.Aviso;
        if (Avalonia.Application.Current is { } app)
        {
            var r = app.Resources;
            r["ColorTexto"] = Texto; r["ColorBoton"] = BotonFondo; r["ColorBotonEncima"] = BotonEncima; r["ColorBotonPulsado"] = BotonPulsado;
            r["ColorBotonTexto"] = BotonTexto; r["ColorSecundario"] = Secundario;
            // el color de lo marcado en los controles del sistema (casillas, opciones, el sexo elegido en la ficha…), el del
            // tema y no el azul de Fluent
            foreach (var k in new[] { "SystemAccentColor", "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3",
                                      "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3" })
                r[k] = t.Destacado;
        }
    }

    public static class Iconos
    {
        public const string Atras = "M15 5 L8 12 L15 19";
        public const string Mas = "M12 5 V19 M5 12 H19";
        public const string Menu = "M12 5.2 a1.2 1.2 0 1 0 0.01 0 M12 11.2 a1.2 1.2 0 1 0 0.01 0 M12 17.2 a1.2 1.2 0 1 0 0.01 0";
        public const string Buscar = "M10.5 4.5 a6 6 0 1 0 0.01 0 M15 15 L20 20";
        public const string Compartir = "M12 15 V4 M7.5 8.5 L12 4 L16.5 8.5 M5 12 V19 H19 V12";
        public const string Editar = "M4 20 L8.5 19 L19 8.5 L15.5 5 L5 15.5 Z M13.5 7 L17 10.5";
        public const string Cerrar = "M6 6 L18 18 M18 6 L6 18";
        public const string Enlace = "M9 15 L18 6 M11 6 H18 V13 M15 18 H6 V9";
        public const string Deshacer = "M9 6 L4 11 L9 16 M4 11 H14 a5 5 0 0 1 0 10 H11";
        public const string Rehacer = "M15 6 L20 11 L15 16 M20 11 H10 a5 5 0 0 0 0 10 H13";
        public const string Camara = "M4 8 H8 L10 5.5 H14 L16 8 H20 V19 H4 Z M12 10.5 a3 3 0 1 0 0.01 0";
        /// <summary>Encuadrar una foto: las dos esquinas de un recorte.</summary>
        public const string Encuadre = "M7 3 V17 H21 M3 7 H17 V21";
        /// <summary>Exportar: una hoja con una flecha que sale hacia la derecha.</summary>
        public const string Exportar = "M13 3 H5 V21 H17 V15 M10 10 H21 M17.5 6.5 L21 10 L17.5 13.5";
        public const string Imprimir = "M7 9 V3.5 H17 V9 M7 17 H4.5 V9 H19.5 V17 H17 M7 13.5 H17 V20.5 H7 Z M16.5 11.5 H16.6";
        public const string Documento = "M6 3 H14 L19 8 V21 H6 Z M14 3 V8 H19 M9 12 H16 M9 15 H16 M9 18 H13";
        public const string Galeria = "M4 5 H20 V19 H4 Z M4 16 L9 11 L13 15 L16 12 L20 16 M15.5 8 a1.3 1.3 0 1 0 0.01 0";
        public const string Encuadrar = "M4 9 V4 H9 M15 4 H20 V9 M20 15 V20 H15 M9 20 H4 V15";
        public const string Carpeta = "M3 6.5 H9.5 L11.5 8.5 H21 V19 H3 Z";
        public const string Fusionar = "M7 4 V8 C7 12 12 12 12 16 V20 M17 4 V8 C17 12 12 12 12 16";
        public const string Papelera = "M5 7 H19 M10 7 V4.5 H14 V7 M7 7 L8 20 H16 L17 7";
        public const string Casa = "M4 11 L12 4 L20 11 M6.5 9.5 V20 H17.5 V9.5";
        public const string Tarjeta = "M3.5 6 H20.5 V18 H3.5 Z M6.5 9 H10.5 V15 H6.5 Z M13 10 H17.5 M13 14 H16";
        public const string Colocacion = "M12 4 V8 M6 12 V8 H18 V12 M6 12 V14 M18 12 V14 M3.5 14 H8.5 V19 H3.5 Z M15.5 14 H20.5 V19 H15.5 Z M9.5 2 H14.5";
        public const string Persona = "M12 4 a4 4 0 1 0 0.01 0 M4.5 20 a7.5 6.5 0 0 1 15 0";
        public const string Guardar = "M5 4 H16 L20 8 V20 H5 Z M8 4 V9 H15 V4 M8 20 V14 H16 V20";
        public const string Nuevo = "M6 3.5 H14 L18.5 8 V20.5 H6 Z M14 3.5 V8 H18.5 M12 11 V17 M9 14 H15";
        public const string Siguiente = "M6 9 L12 15 L18 9";
        public const string Derecha = "M9 5.5 L15.5 12 L9 18.5";

        // Colocaciones del árbol: cajitas (personas) y líneas.
        /// <summary>Compacto: como los árboles impresos, todo lo más junto posible.</summary>
        public const string Compacto = "M7 2.5 H11 V5.5 H7 Z M13 2.5 H17 V5.5 H13 Z M11 4 H13 M12 4 V8 M5.5 8 H18.5 M5.5 8 V10 M12 8 V10 M18.5 8 V10 M3.5 10 H7.5 V13 H3.5 Z M10 10 H14 V13 H10 Z M16.5 10 H20.5 V13 H16.5 Z M12 13 V16 M10 16 H14 V19 H10 Z M3.5 16 H7.5 V19 H3.5 Z M5.5 13 V16";
        /// <summary>Lateral: los hijos de cada pareja cuelgan todos hacia la derecha, generación tras generación.</summary>
        public const string Lateral = "M1.5 4 H5.5 V8 H1.5 Z M7.5 4 H11.5 V8 H7.5 Z M5.5 6 H7.5 M6.5 6 V12 H21 M10 12 V15 M15.5 12 V15 M21 12 V15 M8.25 15 H11.75 V18.5 H8.25 Z M13.75 15 H17.25 V18.5 H13.75 Z M19.25 15 H22.75 V18.5 H19.25 Z";
        /// <summary>Balanceado: los dos lados repartidos, con la persona principal en medio.</summary>
        public const string Balanceado = "M10 2.5 H14 V5.5 H10 Z M12 5.5 V8 M3.5 8 H20.5 M3.5 8 V10.5 M12 8 V10.5 M20.5 8 V10.5 M1.5 10.5 H5.5 V13.5 H1.5 Z M10 10.5 H14 V13.5 H10 Z M18.5 10.5 H22.5 V13.5 H18.5 Z M12 13.5 V16 M7 16 H17 M7 16 V18 M17 16 V18 M5 18 H9 V21 H5 Z M15 18 H19 V21 H15 Z";
        /// <summary>Escalonado: las familias bajan en escalera (el árbol sale estrecho).</summary>
        public const string Escalonado = "M2 2.5 H7 V5.5 H2 Z M4.5 5.5 V9 M4.5 9 H9 M9 7.5 H14 V10.5 H9 Z M11.5 10.5 V14 M11.5 14 H16 M16 12.5 H21 V15.5 H16 Z M18.5 15.5 V18.5 M16 18.5 H21 V21.5 H16 Z";

        // Formas de las tarjetas.
        public const string TarjetaHorizontalFoto = "M2.5 7 H21.5 V17 H2.5 Z M7.5 12 m-2.6 0 a2.6 2.6 0 1 0 5.2 0 a2.6 2.6 0 1 0 -5.2 0 M12.5 10.5 H19 M12.5 13.5 H17";
        public const string TarjetaVerticalFoto = "M6.5 2.5 H17.5 V21.5 H6.5 Z M12 8.5 m-3 0 a3 3 0 1 0 6 0 a3 3 0 1 0 -6 0 M9 14.5 H15 M9.5 17.5 H14.5";
        public const string TarjetaHorizontal = "M2.5 7.5 H21.5 V16.5 H2.5 Z M5.5 7.5 V16.5 M8 10.5 H19 M8 13.5 H16";
        public const string TarjetaVertical = "M7 4 H17 V20 H7 Z M7 6.5 H17 M9.5 11 H14.5 M10 14 H14";
        public const string ConTarjeta = "M2.5 7 H21.5 V17 H2.5 Z M6 10.5 H18 M6 13.5 H14";
        public const string SinTarjeta = "M2.5 9.5 V7 H5.5 M18.5 7 H21.5 V9.5 M21.5 14.5 V17 H18.5 M5.5 17 H2.5 V14.5 M6.5 10.5 H17.5 M6.5 13.5 H13.5";
        /// <summary>Con y sin la marca de historia: una tarjeta con un circulito en la esquina, y sin él.</summary>
        public const string ConHistoria = "M2.5 8 H15.5 M20.5 11.5 V18 H2.5 V8 M6 12 H14 M6 15 H11 M18 5 a3 3 0 1 0 0.01 0";
        public const string SinHistoria = "M2.5 8 H20.5 V18 H2.5 Z M6 12 H14 M6 15 H11";
        public const string ConApellidos = "M4 9.5 H20 M4 14.5 H15";
        /// <summary>Con y sin los años: un calendario, y tachado.</summary>
        public const string ConFechas = "M4.5 6 H19.5 V19.5 H4.5 Z M4.5 10 H19.5 M8.5 3.5 V7.5 M15.5 3.5 V7.5";
        public const string SinFechas = "M4.5 6 H19.5 V19.5 H4.5 Z M4.5 10 H19.5 M8.5 3.5 V7.5 M15.5 3.5 V7.5 M3 21 L21 3";
        public const string SinApellidos = "M4 12 H20";
        public const string Anterior = "M6 15 L12 9 L18 15";
        public const string Info = "M12 3.5 a8.5 8.5 0 1 0 0.01 0 M12 11 V16.5 M12 7.6 V7.7";
        /// <summary>Ramo completo: una estrella.</summary>
        public const string Estrella = "M12 3.5 L14.6 8.9 L20.5 9.6 L16.1 13.6 L17.3 19.5 L12 16.5 L6.7 19.5 L7.9 13.6 L3.5 9.6 L9.4 8.9 Z";
        public const string Hecho = "M5 12.5 L10 17.5 L19 7";
    }

    /// <summary>Un icono de trazo, como los de Material, a partir de un camino en una caja de 24 × 24.</summary>
    public static Control Icono(string datos, double tam = 22, IBrush? color = null, double grosor = 2)
    {
        var path = new Path
        {
            Data = Geometry.Parse(datos), Stroke = color ?? Texto, StrokeThickness = grosor,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round,
        };
        var lienzo = new Canvas { Width = 24, Height = 24, Children = { path } };
        return new Viewbox { Width = tam * EscalaIcono, Height = tam * EscalaIcono, Child = lienzo };
    }

    public static Button BotonIcono(string icono, string ayuda, Action accion, IBrush? color = null)
    {
        var b = new Button { Content = Icono(icono, 22, color), Classes = { "plano" } };
        ToolTip.SetTip(b, ayuda);
        Avalonia.Automation.AutomationProperties.SetName(b, ayuda);
        b.Click += (_, _) => accion();
        return b;
    }

    public static Button Boton(string texto, Action accion, bool principal = false, string? icono = null)
    {
        // el texto puede pasar a dos líneas si no cabe (con la letra muy grande)
        var letras = new TextBlock
        {
            Text = texto, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            Foreground = principal ? BotonTexto : Texto, FontWeight = principal ? FontWeight.SemiBold : FontWeight.Normal,
        };
        Control contenido = letras;
        if (icono != null)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), HorizontalAlignment = HorizontalAlignment.Center };
            var ic = Icono(icono, 20, principal ? BotonTexto : Texto);
            ic.Margin = new Thickness(0, 0, 8, 0); ic.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(ic);
            Grid.SetColumn(letras, 1); g.Children.Add(letras);
            contenido = g;
        }
        var b = new Button { Content = contenido, Classes = { principal ? "principal" : "secundario" } };
        b.Click += (_, _) => accion();
        return b;
    }

    public static TextBlock Titulo(string s, double tam = 20) =>
        new() { Text = s, FontSize = L(tam), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };

    public static TextBlock Parrafo(string s, double tam = 15, IBrush? color = null) =>
        new() { Text = s, FontSize = L(tam), Foreground = color ?? Suave, TextWrapping = TextWrapping.Wrap, LineHeight = L(tam) * 1.4 };

    /// <summary>
    /// La muestra de un tema de colores para elegirlo: un trocito de árbol con sus colores (el fondo, una tarjeta de hombre de
    /// la línea directa, una de mujer y la línea que las une).
    /// </summary>
    public static Control MuestraTema(Dibujo.Tema t)
    {
        static IBrush P(Color c) => new SolidColorBrush(c);
        Border Tarjeta(Dibujo.Estilo e, Color borde, double x, double y) => new()
        {
            Width = 30, Height = 13, CornerRadius = new CornerRadius(e.Radio / 4), Background = P(e.Fondo2),
            BorderBrush = P(borde), BorderThickness = new Thickness(Math.Max(1.2, t.GrosorBorde * 0.8)),
            [Canvas.LeftProperty] = x, [Canvas.TopProperty] = y,
        };
        var linea = new Path
        {
            Data = Geometry.Parse("M21 19 V24.5 H39 V30"), Stroke = P(t.Linaje), StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round,
        };
        var lienzo = new Canvas
        {
            Width = 60, Height = 44,
            Children = { linea, Tarjeta(t.Hombre, t.Directa, 6, 6), Tarjeta(t.Mujer, t.Mujer.Borde, 24, 30) },
        };
        return new Border
        {
            Width = 62 * EscalaIcono, Height = 46 * EscalaIcono, CornerRadius = new CornerRadius(9), ClipToBounds = true,
            BorderBrush = P(t.Claro ? Color.FromRgb(0xB8, 0xBE, 0xCA) : t.Linea), BorderThickness = new Thickness(1),
            Background = new RadialGradientBrush { GradientStops = { new GradientStop(t.Lienzo1, 0), new GradientStop(t.Lienzo2, 1) } },
            Child = new Viewbox { Child = lienzo },
        };
    }

    /// <summary>La muestra de una letra de las tarjetas: «Aa» con ella, del mismo tamaño que la de un tema.</summary>
    public static Control MuestraLetra(Dibujo.Letra l) => new Border
    {
        Width = 62 * EscalaIcono, Height = 46 * EscalaIcono, CornerRadius = new CornerRadius(9),
        BorderBrush = Linea, BorderThickness = new Thickness(1), Background = Panel,
        Child = new TextBlock
        {
            Text = "Aa", FontFamily = l.Familia, FontWeight = l.PesoNombre, FontSize = 25 * EscalaIcono * l.Escala, Foreground = Texto,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        },
    };

    /// <summary>Un círculo con la foto de la persona o sus iniciales.</summary>
    public static Control Retrato(Core.Model.Persona p, double tam)
    {
        var paleta = Dibujo.Paleta.Pantalla;
        var e = paleta.Estilo(p.Sexo, Core.Presentacion.Nombres.SinNombre(p));
        var foto = Fotos.Imagen(p.Foto);
        Control dentro = foto != null
            ? new Image { Source = foto, Stretch = Stretch.UniformToFill, Width = tam, Height = tam }
            : new TextBlock
            {
                Text = Core.Presentacion.Nombres.Iniciales(p), FontSize = tam * 0.36, FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = paleta.Iniciales(e),
            };
        return new Border
        {
            Width = tam, Height = tam, CornerRadius = new CornerRadius(tam / 2), ClipToBounds = true,
            Background = paleta.SinFoto(e),
            BorderBrush = new SolidColorBrush(e.Acento), BorderThickness = new Thickness(2),
            Child = dentro,
        };
    }
}
