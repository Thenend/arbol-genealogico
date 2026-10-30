using Avalonia.Media;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>Cómo sale el árbol en papel: como en pantalla (con los colores del tema), claro (poca tinta) o solo con tinta negra.</summary>
public enum EstiloPapel { Oscuro, Claro, BlancoYNegro }

/// <summary>
/// Los colores del árbol: los de la pantalla (los del <see cref="Tema"/> elegido) o, en papel, los de cada estilo (los
/// mismos que al imprimir en el escritorio, con los colores de Noche). En el claro, las tarjetas son blancas con un tinte de
/// su color, y el color, más oscuro, va en el borde, la franja y el aro de la foto; en blanco y negro todo va en negro o en
/// grises bien distintos (el sexo se ve en la forma de la tarjeta y en el tono de su franja).
/// </summary>
internal sealed class Paleta
{
    public EstiloPapel Tipo { get; private init; }
    /// <summary>El tema de la pantalla (en papel claro o en blanco y negro, null).</summary>
    private Tema? Tema { get; init; }
    private bool ConClaro => Tipo != EstiloPapel.Oscuro;
    private bool ByN => Tipo == EstiloPapel.BlancoYNegro;
    /// <summary>Con fondo claro: en papel o en un tema claro.</summary>
    private bool FondoClaro => Tema?.Claro ?? true;

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
    /// <summary>El temblor de los trazos «a mano» (solo en pantalla o «como en pantalla», con el tema Papel y tinta).</summary>
    public double AMano => Tema?.AMano ?? 0;
    public double AManoLineas => Tema?.AManoLineas ?? 0;
    public Color? Lazo => Tema?.Lazo;
    // los de la prueba de estilos completos (solo con un tema: en papel claro o en blanco y negro, como siempre)
    public bool SinFranja => Tema?.SinFranja ?? false;
    public Color? Doble => Tema?.Doble;
    public bool Acuarela => Tema?.Acuarela ?? false;
    public double RadioLineas => Tema?.RadioLineas ?? 0;
    public Color? Hojas => Tema?.Hojas;
    public string Nudo => Tema?.Nudo ?? "circulo";
    public Color? ColorNudo => Tema?.ColorNudo;
    public bool SinSombra => Tema?.SinSombra ?? false;
    public bool Plumilla => Tema?.Plumilla ?? false;
    public byte AlfaSombra => FondoClaro ? (byte)0x16 : (byte)0x55;
    public Color Seleccion => Tema?.Seleccion ?? Color.FromRgb(0x1E, 0x25, 0x33);
    public Color HaloSeleccion => Tema?.HaloSeleccion ?? Color.FromArgb(0x18, 0x1E, 0x25, 0x33);
    public Color Busqueda => Tema?.Busqueda ?? Dibujo.Estilo.Lima;
    /// <summary>El fondo de la marca de la historia y de los círculos de las parejas.</summary>
    public Color FondoMarca => Tema is { Claro: false } t ? t.Fondo : Colors.White;
    /// <summary>El fondo detrás del árbol en la imagen y el PDF que se comparten.</summary>
    public Color FondoImagen => Tema?.Fondo ?? Colors.White;
    public Color TextoMarca => Dibujo.Estilo.ConAlfa(Tema?.Suave ?? Color.FromRgb(0x9A, 0xA2, 0xB6), 0xB0);

    // ---------- Letras ----------

    public Typeface LetraNombre { get; private set; }
    public Typeface LetraNombreCursiva { get; private set; }
    public Typeface LetraApellidos { get; private set; }
    /// <summary>Cuánto más grandes van las letras de las tarjetas (las de la letra elegida, ver <see cref="Letra"/>).</summary>
    public double EscalaLetra { get; private set; } = 1;
    public double EscalaApellidos { get; private set; } = 1;

    private static Pen Lapiz(Color c, byte alfa, double grosor) =>
        new(new SolidColorBrush(Dibujo.Estilo.ConAlfa(c, alfa)), grosor) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };

    /// <summary>Las letras de las tarjetas con la letra de ahora (en el nombre, al menos tan gruesa como pide el tema).</summary>
    private Paleta ConLetra(FontWeight pesoMinimo)
    {
        var l = Letra.Actual;
        var peso = (FontWeight)Math.Max((int)l.PesoNombre, (int)pesoMinimo);
        LetraNombre = new Typeface(l.Familia, l.Cursiva ? FontStyle.Italic : FontStyle.Normal, peso);
        LetraNombreCursiva = new Typeface(l.Familia, FontStyle.Italic, peso);
        LetraApellidos = new Typeface(l.FamiliaApellidos ?? l.Familia, l.ApellidosCursiva ? FontStyle.Italic : FontStyle.Normal, l.PesoApellidos);
        EscalaLetra = l.Escala;
        EscalaApellidos = l.EscalaApellidos ?? l.Escala;
        return this;
    }

    /// <summary>En papel claro o en blanco y negro.</summary>
    private static Paleta Crear(EstiloPapel estilo)
    {
        bool bn = estilo == EstiloPapel.BlancoYNegro;
        var gris = bn ? Color.FromRgb(0x80, 0x80, 0x80) : Color.FromRgb(0x7C, 0x84, 0x96);
        var pareja = bn ? Color.FromRgb(0x5C, 0x5C, 0x5C) : Color.FromRgb(0x6A, 0x72, 0x86);
        var oro = bn ? Colors.Black : OroOscuro;
        // sobre papel blanco, el turquesa del linaje, más oscuro para que se vea
        var linaje = Color.FromRgb(0x0F, 0x9C, 0x8C);
        return new Paleta
        {
            Tipo = estilo,
            Normal = new[] { Lapiz(gris, 0xFF, 2), Lapiz(gris, 0x55, 2) },
            Pareja = new[] { Lapiz(pareja, 0xFF, 2.5), Lapiz(pareja, 0x55, 2.5) },
            Oro = new[] { Lapiz(oro, 0xFF, 3.6), Lapiz(oro, 0x80, 3.6) },
            Resplandor = bn ? null : new[] { Lapiz(Dibujo.Estilo.Oro, 0x2C, 9), Lapiz(Dibujo.Estilo.Oro, 0x10, 9) },
            Luz = Lapiz(linaje, 0xFF, 4), LuzResplandor = Lapiz(linaje, 0x40, 11),
            FondoNudo = Brushes.White,
            ColorPareja = pareja, ColorOro = oro, ColorLinaje = linaje,
            Apellidos = new SolidColorBrush(bn ? Color.FromRgb(0x3A, 0x3A, 0x3A) : Color.FromRgb(0x5A, 0x61, 0x72)),
            Fechas = new SolidColorBrush(bn ? Color.FromRgb(0x55, 0x55, 0x55) : Color.FromRgb(0x70, 0x76, 0x86)),
        }.ConLetra(FontWeight.SemiBold);
    }

    /// <summary>En la pantalla, con los colores del tema (y en papel, «como en pantalla»).</summary>
    private static Paleta Crear(Tema t)
    {
        double k = t.GrosorLinea / 2;
        var brillo = t.BrilloDirecta;
        return new Paleta
        {
            Tipo = EstiloPapel.Oscuro, Tema = t,
            Normal = new[] { Lapiz(t.LineaArbol, 0xFF, 2 * k), Lapiz(t.LineaArbol, 0x55, 2 * k) },
            Pareja = new[] { Lapiz(t.Pareja, 0xFF, 2.5 * k), Lapiz(t.Pareja, 0x55, 2.5 * k) },
            Oro = new[] { Lapiz(t.Directa, 0xFF, 3.6 * k), Lapiz(t.Directa, 0x80, 3.6 * k) },
            Resplandor = brillo is { } b ? new[] { Lapiz(b, b.A, 9), Lapiz(b, (byte)(b.A * 0x10 / 0x2C), 9) } : null,
            Luz = Lapiz(t.Linaje, 0xFF, 4 * Math.Max(1, k)),
            LuzResplandor = Lapiz(t.BrilloLinaje, t.BrilloLinaje.A, 11),
            FondoNudo = new SolidColorBrush(t.Claro ? Colors.White : t.Fondo),
            ColorPareja = t.Pareja, ColorOro = t.Directa, ColorLinaje = t.Linaje,
            Apellidos = new SolidColorBrush(t.Apellidos),
            Fechas = new SolidColorBrush(t.Fechas),
        }.ConLetra(t.PesoNombre);
    }

    // antes que las paletas, que lo usan al crearse
    private static readonly Color OroOscuro = Color.FromRgb(0xC2, 0x8A, 0x12);

    private static Paleta? _pantalla;
    /// <summary>La de la pantalla, con el tema de ahora.</summary>
    public static Paleta Pantalla => _pantalla ??= Crear(Dibujo.Tema.Actual);
    private static Paleta? _papelClaro, _papelByN;

    /// <summary>Al cambiar de tema o de letra, se vuelven a hacer (las de papel llevan la letra de ahora).</summary>
    public static void CambiarTema() => _pantalla = _papelClaro = _papelByN = null;

    public static Paleta De(EstiloPapel e) => e switch
    {
        EstiloPapel.Claro => _papelClaro ??= Crear(EstiloPapel.Claro),
        EstiloPapel.BlancoYNegro => _papelByN ??= Crear(EstiloPapel.BlancoYNegro),
        _ => Pantalla,
    };

    /// <summary>Los colores y la forma de la tarjeta de una persona (en papel claro o en blanco y negro, los de Noche).</summary>
    public Estilo Estilo(Sexo s, bool sinNombre) => Tema?.De(s, sinNombre) ?? Dibujo.Estilo.De(s, sinNombre);

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
        var tinta = ByN ? Colors.Black : Tema?.TintaNombre ?? Color.FromRgb(0x1C, 0x20, 0x2C);
        if (sinNombre) return new SolidColorBrush(Dibujo.Estilo.ConAlfa(tinta, 0x8C));
        if (sinTarjeta)
            return new SolidColorBrush(ByN ? Colors.Black : ConClaro ? Oscurecer(e.Acento, 0.4) : FondoClaro ? Oscurecer(e.Acento, 0.1) : Aclarar(e.Acento, 0.15));
        return new SolidColorBrush(tinta);
    }

    /// <summary>El círculo de la foto de quien no tiene foto, y sus iniciales.</summary>
    public IBrush SinFoto(Estilo e) => new SolidColorBrush(ByN ? Color.FromRgb(0xE6, 0xE6, 0xE6)
        : ConClaro ? Aclarar(e.Acento, 0.7) : FondoClaro ? Aclarar(e.Acento, 0.78) : Dibujo.Estilo.ConAlfa(e.Acento, 0x55));
    public IBrush Iniciales(Estilo e) => ByN ? Brushes.Black
        : ConClaro ? new SolidColorBrush(Oscurecer(e.Acento, 0.45)) : FondoClaro ? new SolidColorBrush(Oscurecer(e.Acento, 0.2)) : Brushes.White;

    /// <summary>El contorno de una tarjeta de la línea directa y el de las demás.</summary>
    public (Color Color, double Grosor) BordeDirecta => ByN ? (Colors.Black, 2.8) : (ColorOro, Tema?.GrosorBordeDirecta ?? 2.5);
    public (Color Color, double Grosor) Borde(Estilo e) =>
        ByN ? (Color.FromRgb(0x8C, 0x8C, 0x8C), 1.3) : ConClaro ? (Oscurecer(e.Acento, 0.2), 1.3) : (e.Borde, Tema!.GrosorBorde);
    /// <summary>El brillo alrededor de las tarjetas de la línea directa (en blanco y negro, ninguno).</summary>
    public IBrush? HaloDirecta => ByN ? null
        : new SolidColorBrush(Tema?.HaloDirecta ?? Dibujo.Estilo.ConAlfa(Dibujo.Estilo.Oro, 0x30));

    public static Color Aclarar(Color c, double t) =>
        Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));
    public static Color Oscurecer(Color c, double t) =>
        Color.FromRgb((byte)(c.R * (1 - t)), (byte)(c.G * (1 - t)), (byte)(c.B * (1 - t)));
}
