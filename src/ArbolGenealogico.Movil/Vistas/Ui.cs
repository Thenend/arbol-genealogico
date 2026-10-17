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

    public static readonly IBrush Fondo = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x16));
    public static readonly IBrush Barra = new SolidColorBrush(Color.FromRgb(0x15, 0x19, 0x25));
    public static readonly IBrush Panel = new SolidColorBrush(Color.FromRgb(0x1B, 0x20, 0x2E));
    public static readonly IBrush Linea = new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x42));
    public static readonly IBrush Texto = new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xF0));
    public static readonly IBrush Suave = new SolidColorBrush(Color.FromRgb(0x9A, 0xA2, 0xB6));
    public static readonly IBrush Oro = new SolidColorBrush(Color.FromRgb(0xF5, 0xC4, 0x51));
    public static readonly IBrush Rojo = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0x7A));

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
        public const string Anterior = "M6 15 L12 9 L18 15";
        public const string Info = "M12 3.5 a8.5 8.5 0 1 0 0.01 0 M12 11 V16.5 M12 7.6 V7.7";
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
            Foreground = principal ? new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x05)) : Texto, FontWeight = principal ? FontWeight.SemiBold : FontWeight.Normal,
        };
        Control contenido = letras;
        if (icono != null)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), HorizontalAlignment = HorizontalAlignment.Center };
            var ic = Icono(icono, 20, principal ? new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x05)) : Texto);
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

    /// <summary>Un círculo con la foto de la persona o sus iniciales.</summary>
    public static Control Retrato(Core.Model.Persona p, double tam)
    {
        var e = Dibujo.Estilo.De(p.Sexo, Core.Presentacion.Nombres.SinNombre(p));
        var foto = Fotos.Imagen(p.Foto);
        Control dentro = foto != null
            ? new Image { Source = foto, Stretch = Stretch.UniformToFill, Width = tam, Height = tam }
            : new TextBlock
            {
                Text = Core.Presentacion.Nombres.Iniciales(p), FontSize = tam * 0.36, FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White,
            };
        return new Border
        {
            Width = tam, Height = tam, CornerRadius = new CornerRadius(tam / 2), ClipToBounds = true,
            Background = new SolidColorBrush(Dibujo.Estilo.ConAlfa(e.Acento, 0x55)),
            BorderBrush = new SolidColorBrush(e.Acento), BorderThickness = new Thickness(2),
            Child = dentro,
        };
    }
}
