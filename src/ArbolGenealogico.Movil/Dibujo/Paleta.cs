using Avalonia.Media;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>Cómo sale el árbol en papel: como en pantalla (oscuro), claro (poca tinta) o solo con tinta negra.</summary>
public enum EstiloPapel { Oscuro, Claro, BlancoYNegro }

/// <summary>
/// Los colores del árbol: los de la pantalla o, en papel, los de cada estilo (los mismos que al imprimir en el escritorio).
/// En el claro, las tarjetas son blancas con un tinte de su color, y el color, más oscuro, va en el borde, la franja y el aro
/// de la foto; en blanco y negro todo va en negro o en grises bien distintos (el sexo se ve en la forma de la tarjeta y en el
/// tono de su franja).
/// </summary>
internal sealed class Paleta
{
    public EstiloPapel Tipo { get; private init; }
    private bool ConClaro => Tipo != EstiloPapel.Oscuro;
    private bool ByN => Tipo == EstiloPapel.BlancoYNegro;

    // ---------- Líneas ----------

    /// <summary>[0] = con normalidad, [1] = atenuada.</summary>
    public Pen[] Normal { get; private init; } = null!;
    public Pen[] Pareja { get; private init; } = null!;
    public Pen[] Oro { get; private init; } = null!;
    /// <summary>El brillo de la línea directa (en blanco y negro no: sería una mancha gris).</summary>
    public Pen[]? Resplandor { get; private init; }
    public Pen Luz { get; private init; } = null!;
    public Pen LuzResplandor { get; private init; } = null!;
    public IBrush FondoNudo { get; private init; } = null!;
    public Color ColorPareja { get; private init; }
    public Color ColorOro { get; private init; }
    public Color ColorLinaje { get; private init; }

    // ---------- Tarjetas ----------

    public IBrush Apellidos { get; private init; } = null!;
    public IBrush Fechas { get; private init; } = null!;
    public byte AlfaSombra => ConClaro ? (byte)0x16 : (byte)0x55;
    public Color Seleccion => ConClaro ? Color.FromRgb(0x1E, 0x25, 0x33) : Colors.White;
    /// <summary>El fondo de la marca de la historia y de los círculos de las parejas.</summary>
    public Color FondoMarca => ConClaro ? Colors.White : Dibujo.Estilo.FondoApp;

    private static Pen Lapiz(Color c, byte alfa, double grosor) =>
        new(new SolidColorBrush(Dibujo.Estilo.ConAlfa(c, alfa)), grosor) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };

    private static Paleta Crear(EstiloPapel estilo)
    {
        bool claro = estilo != EstiloPapel.Oscuro, bn = estilo == EstiloPapel.BlancoYNegro;
        var gris = bn ? Color.FromRgb(0x80, 0x80, 0x80) : claro ? Color.FromRgb(0x7C, 0x84, 0x96) : Color.FromRgb(0x5B, 0x65, 0x7D);
        var pareja = bn ? Color.FromRgb(0x5C, 0x5C, 0x5C) : claro ? Color.FromRgb(0x6A, 0x72, 0x86) : Color.FromRgb(0x8A, 0x94, 0xAD);
        var oro = bn ? Colors.Black : claro ? OroOscuro : Dibujo.Estilo.Oro;
        // sobre papel blanco, el turquesa del linaje, más oscuro para que se vea
        var linaje = claro ? Color.FromRgb(0x0F, 0x9C, 0x8C) : Dibujo.Estilo.Linaje;
        return new Paleta
        {
            Tipo = estilo,
            Normal = new[] { Lapiz(gris, 0xFF, 2), Lapiz(gris, 0x55, 2) },
            Pareja = new[] { Lapiz(pareja, 0xFF, 2.5), Lapiz(pareja, 0x55, 2.5) },
            Oro = new[] { Lapiz(oro, 0xFF, 3.6), Lapiz(oro, 0x80, 3.6) },
            Resplandor = bn ? null : new[] { Lapiz(Dibujo.Estilo.Oro, 0x2C, 9), Lapiz(Dibujo.Estilo.Oro, 0x10, 9) },
            Luz = Lapiz(linaje, 0xFF, 4), LuzResplandor = Lapiz(linaje, 0x40, 11),
            FondoNudo = new SolidColorBrush(claro ? Colors.White : Dibujo.Estilo.FondoApp),
            ColorPareja = pareja, ColorOro = oro, ColorLinaje = linaje,
            Apellidos = new SolidColorBrush(bn ? Color.FromRgb(0x3A, 0x3A, 0x3A) : claro ? Color.FromRgb(0x5A, 0x61, 0x72) : Color.FromRgb(0xB4, 0xBA, 0xCB)),
            Fechas = new SolidColorBrush(bn ? Color.FromRgb(0x55, 0x55, 0x55) : claro ? Color.FromRgb(0x70, 0x76, 0x86) : Color.FromRgb(0x95, 0x9D, 0xB2)),
        };
    }

    // antes que las paletas, que lo usan al crearse
    private static readonly Color OroOscuro = Color.FromRgb(0xC2, 0x8A, 0x12);

    public static readonly Paleta Pantalla = Crear(EstiloPapel.Oscuro);
    private static readonly Paleta PapelClaro = Crear(EstiloPapel.Claro), PapelByN = Crear(EstiloPapel.BlancoYNegro);

    public static Paleta De(EstiloPapel e) => e switch
    {
        EstiloPapel.Claro => PapelClaro, EstiloPapel.BlancoYNegro => PapelByN, _ => Pantalla,
    };

    /// <summary>El color de la persona con el que se dibujan su franja, el aro de su foto y su marca de la historia.</summary>
    public Color Acento(Estilo e, Sexo s, bool sinNombre) =>
        ByN ? (sinNombre ? Color.FromRgb(0xBC, 0xBC, 0xBC)
               : s switch { Sexo.Hombre => Color.FromRgb(0x14, 0x14, 0x14), Sexo.Mujer => Color.FromRgb(0xA4, 0xA4, 0xA4), _ => Color.FromRgb(0x62, 0x62, 0x62) })
        : ConClaro ? Oscurecer(e.Acento, 0.25) : e.Acento;

    public IBrush Fondo(Estilo e) => ByN ? Brushes.White : new LinearGradientBrush
    {
        StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative), EndPoint = new Avalonia.RelativePoint(0, 1, Avalonia.RelativeUnit.Relative),
        GradientStops = ConClaro
            ? new GradientStops { new GradientStop(Colors.White, 0), new GradientStop(Aclarar(e.Acento, 0.88), 1) }
            : new GradientStops { new GradientStop(e.Fondo1, 0), new GradientStop(e.Fondo2, 1) },
    };

    /// <summary>El color del nombre: sobre la tarjeta, sin tarjeta (del color de su sexo) o el «Sin nombre», apagado.</summary>
    public IBrush Nombre(Estilo e, bool sinNombre, bool sinTarjeta)
    {
        var tinta = ByN ? Colors.Black : ConClaro ? Color.FromRgb(0x1C, 0x20, 0x2C) : Colors.White;
        if (sinNombre) return new SolidColorBrush(Dibujo.Estilo.ConAlfa(tinta, 0x8C));
        if (sinTarjeta) return new SolidColorBrush(ByN ? Colors.Black : ConClaro ? Oscurecer(e.Acento, 0.4) : Aclarar(e.Acento, 0.15));
        return ConClaro ? new SolidColorBrush(tinta) : Brushes.White;
    }

    /// <summary>El círculo de la foto de quien no tiene foto, y sus iniciales.</summary>
    public IBrush SinFoto(Estilo e) => new SolidColorBrush(ByN ? Color.FromRgb(0xE6, 0xE6, 0xE6)
        : ConClaro ? Aclarar(e.Acento, 0.7) : Dibujo.Estilo.ConAlfa(e.Acento, 0x55));
    public IBrush Iniciales(Estilo e) => ByN ? Brushes.Black : ConClaro ? new SolidColorBrush(Oscurecer(e.Acento, 0.45)) : Brushes.White;

    /// <summary>El contorno de una tarjeta de la línea directa y el de las demás.</summary>
    public (Color Color, double Grosor) BordeDirecta => ByN ? (Colors.Black, 2.8) : (ColorOro, 2.5);
    public (Color Color, double Grosor) Borde(Estilo e) =>
        ByN ? (Color.FromRgb(0x8C, 0x8C, 0x8C), 1.3) : ConClaro ? (Oscurecer(e.Acento, 0.2), 1.3) : (e.Borde, 1.5);
    /// <summary>El brillo alrededor de las tarjetas de la línea directa (en blanco y negro, ninguno).</summary>
    public IBrush? HaloDirecta => ByN ? null : new SolidColorBrush(Dibujo.Estilo.ConAlfa(Dibujo.Estilo.Oro, ConClaro ? (byte)0x30 : (byte)0x26));

    public static Color Aclarar(Color c, double t) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
    public static Color Oscurecer(Color c, double t) =>
        Color.FromRgb((byte)(c.R * (1 - t)), (byte)(c.G * (1 - t)), (byte)(c.B * (1 - t)));
}
