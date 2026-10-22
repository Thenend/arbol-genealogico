using Avalonia.Media;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// Los colores de la aplicación y del árbol en la pantalla: Jardín (verdes, con las líneas como ramas; el de la primera vez),
/// Noche (el oscuro del principio), Día (claro), Álbum (sepia, con letra de libro) y Alto contraste (negro sobre blanco, para
/// leer sin esfuerzo).
/// En todos, el sexo se ve en el color y en la forma de la tarjeta (las esquinas, las mismas en todos). Al imprimir, los
/// estilos de papel «Claro» y «Blanco y negro» no dependen del tema; el tercero es «como en pantalla».
/// </summary>
public sealed class Tema
{
    /// <summary>Lo que se guarda en las preferencias («noche», «dia»…).</summary>
    public required string Id { get; init; }
    /// <summary>El nombre, en el idioma de ahora.</summary>
    public string Nombre => Id switch
    {
        "dia" => T("Día"), "album" => T("Álbum"), "jardin" => T("Jardín"), "contraste" => T("Alto contraste"), _ => T("Noche"),
    };
    /// <summary>Cómo es, en una línea.</summary>
    public string Detalle => Id switch
    {
        "dia" => T("Claro, con tarjetas blancas"),
        "album" => T("Sepia, como un álbum de fotos antiguo"),
        "jardin" => T("Verdes, con las líneas como ramas"),
        "contraste" => T("Negro sobre blanco, para leer sin esfuerzo"),
        _ => T("Oscuro, con la línea directa en dorado"),
    };
    /// <summary>Fondo claro: los controles del sistema (campos de texto, barra de estado…) en su versión clara.</summary>
    public required bool Claro { get; init; }

    // ---------- La aplicación ----------

    public required Color Fondo { get; init; }
    public required Color Barra { get; init; }
    public required Color Panel { get; init; }
    public required Color Linea { get; init; }
    public required Color Texto { get; init; }
    public required Color Suave { get; init; }
    /// <summary>Lo elegido en los menús (texto, dibujo y borde) y el fondo del segmento marcado.</summary>
    public required Color Destacado { get; init; }
    /// <summary>El texto sobre <see cref="Destacado"/>.</summary>
    public required Color SobreDestacado { get; init; }
    /// <summary>El botón principal («+ Familiar», «Aceptar»…): su fondo, al pasar por encima, al pulsarlo y su texto.</summary>
    public required Color Boton { get; init; }
    public required Color BotonEncima { get; init; }
    public required Color BotonPulsado { get; init; }
    public required Color BotonTexto { get; init; }
    /// <summary>El fondo de los botones secundarios y de los mosaicos de los menús.</summary>
    public required Color Secundario { get; init; }
    /// <summary>El fondo de los avisos breves de abajo (con el texto en blanco).</summary>
    public required Color Aviso { get; init; }
    public required Color Rojo { get; init; }
    public required Color Verde { get; init; }

    // ---------- El árbol ----------

    /// <summary>El fondo del árbol: un degradado del centro (1) a los bordes (2).</summary>
    public required Color Lienzo1 { get; init; }
    public required Color Lienzo2 { get; init; }
    public required Estilo Hombre { get; init; }
    public required Estilo Mujer { get; init; }
    public required Estilo Otro { get; init; }
    public required Estilo SinNombre { get; init; }
    /// <summary>Las líneas de padres a hijos y las de las parejas.</summary>
    public required Color LineaArbol { get; init; }
    public required Color Pareja { get; init; }
    /// <summary>La línea directa (las tarjetas y las líneas de los antepasados y descendientes de la persona principal).</summary>
    public required Color Directa { get; init; }
    /// <summary>La línea de la persona seleccionada, y su brillo.</summary>
    public required Color Linaje { get; init; }
    public required Color BrilloLinaje { get; init; }
    /// <summary>Las que coinciden con la búsqueda.</summary>
    public required Color Busqueda { get; init; }
    /// <summary>El contorno de la seleccionada, y el halo a su alrededor.</summary>
    public required Color Seleccion { get; init; }
    public required Color HaloSeleccion { get; init; }
    /// <summary>El halo de las tarjetas de la línea directa.</summary>
    public required Color HaloDirecta { get; init; }
    /// <summary>El brillo de las líneas de la línea directa (null: sin brillo).</summary>
    public Color? BrilloDirecta { get; init; }
    /// <summary>La tinta del nombre, los apellidos y los años en las tarjetas.</summary>
    public required Color TintaNombre { get; init; }
    public required Color Apellidos { get; init; }
    public required Color Fechas { get; init; }
    public double GrosorLinea { get; init; } = 2;
    public double GrosorBorde { get; init; } = 1.5;
    public double GrosorBordeDirecta { get; init; } = 2.5;
    /// <summary>La letra del nombre y la de los apellidos y los años (con o sin cursiva).</summary>
    public FontFamily LetraNombre { get; init; } = Estilo.Letra;
    public FontWeight PesoNombre { get; init; } = FontWeight.SemiBold;
    public FontFamily LetraApellidos { get; init; } = Estilo.Letra;
    public bool ApellidosCursiva { get; init; }

    public Estilo De(Core.Model.Sexo s, bool sinNombre)
    {
        var e = s switch { Core.Model.Sexo.Hombre => Hombre, Core.Model.Sexo.Mujer => Mujer, _ => Otro };
        return sinNombre ? SinNombre.ConRadio(e.Radio) : e;
    }

    private static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    private static Estilo E(uint f1, uint f2, uint borde, uint acento, double radio) =>
        new() { Fondo1 = C(f1), Fondo2 = C(f2), Borde = C(borde), Acento = C(acento), Radio = radio };

    // las esquinas, las de siempre: los hombres, poco redondeadas; las mujeres, mucho; sin especificar, a medio camino
    private const double RH = 9, RM = 26, RO = 16;

    public static readonly Tema Noche = new()
    {
        Id = "noche", Claro = false,
        Fondo = C(0x0E1016), Barra = C(0x151925), Panel = C(0x1B202E), Linea = C(0x2A3042), Texto = C(0xE8EAF0), Suave = C(0x9AA2B6),
        Destacado = C(0xF5C451), SobreDestacado = C(0x1A1405),
        Boton = C(0xF5C451), BotonEncima = C(0xFFD36B), BotonPulsado = C(0xD9A93A), BotonTexto = C(0x1A1405),
        Secundario = C(0x262B3A), Aviso = C(0x2E3448), Rojo = C(0xFF7A7A), Verde = C(0x7BD88F),
        Lienzo1 = C(0x161A26), Lienzo2 = C(0x0A0C11),
        Hombre = Estilo.Hombre, Mujer = Estilo.Mujer, Otro = Estilo.Otro,
        SinNombre = E(0x2D313E, 0x20232D, 0x5A6072, 0xA3AABE, RO),
        LineaArbol = C(0x5B657D), Pareja = C(0x8A94AD), Directa = Estilo.Oro, Linaje = Estilo.Linaje, BrilloLinaje = Estilo.ConAlfa(Estilo.Linaje, 0x40),
        Busqueda = Estilo.Lima, Seleccion = Colors.White, HaloSeleccion = Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF),
        HaloDirecta = Estilo.ConAlfa(Estilo.Oro, 0x26), BrilloDirecta = Estilo.ConAlfa(Estilo.Oro, 0x2C),
        TintaNombre = Colors.White, Apellidos = C(0xB4BACB), Fechas = C(0x959DB2),
    };

    public static readonly Tema Dia = new()
    {
        Id = "dia", Claro = true,
        Fondo = C(0xF3F5F9), Barra = C(0xFFFFFF), Panel = C(0xFFFFFF), Linea = C(0xDCE1EA), Texto = C(0x1C202C), Suave = C(0x5A6172),
        Destacado = C(0xA36B00), SobreDestacado = C(0xFFFFFF),
        Boton = C(0xF5C451), BotonEncima = C(0xFFD36B), BotonPulsado = C(0xD9A93A), BotonTexto = C(0x1A1405),
        Secundario = C(0xE5E9F0), Aviso = C(0x2E3448), Rojo = C(0xC62828), Verde = C(0x2E7D32),
        Lienzo1 = C(0xF8FAFC), Lienzo2 = C(0xE8ECF2),
        Hombre = E(0xFFFFFF, 0xE3EBFB, 0x5B82CC, 0x4A6FB8, RH),
        Mujer = E(0xFFFFFF, 0xFBE5F0, 0xC46A9A, 0xB0548A, RM),
        Otro = E(0xFFFFFF, 0xECE6FB, 0x8A70C8, 0x7458B8, RO),
        SinNombre = E(0xFFFFFF, 0xEDEFF3, 0x9AA0AE, 0x858C9D, RO),
        LineaArbol = C(0x8A92A4), Pareja = C(0x6A7286), Directa = C(0xC28A12), Linaje = C(0x0F9C8C), BrilloLinaje = Color.FromArgb(0x30, 0x0F, 0x9C, 0x8C),
        Busqueda = C(0x5E9F0A), Seleccion = C(0x1E2533), HaloSeleccion = Color.FromArgb(0x18, 0x1E, 0x25, 0x33),
        HaloDirecta = Estilo.ConAlfa(Estilo.Oro, 0x30), BrilloDirecta = Estilo.ConAlfa(Estilo.Oro, 0x30),
        TintaNombre = C(0x1C202C), Apellidos = C(0x5A6172), Fechas = C(0x707686), GrosorBorde = 1.3,
    };

    public static readonly Tema Album = new()
    {
        Id = "album", Claro = true,
        Fondo = C(0xEFE6D2), Barra = C(0xE4D7BA), Panel = C(0xF8F2E4), Linea = C(0xD6C7A6), Texto = C(0x3A2A1A), Suave = C(0x76624B),
        Destacado = C(0x8A5A1E), SobreDestacado = C(0xFCF8EE),
        Boton = C(0x6B4A2B), BotonEncima = C(0x7E5A37), BotonPulsado = C(0x573B21), BotonTexto = C(0xFCF8EE),
        Secundario = C(0xE6DAC0), Aviso = C(0x3A2A1A), Rojo = C(0xA33A2A), Verde = C(0x4F7A2F),
        Lienzo1 = C(0xF6EEDC), Lienzo2 = C(0xE6D9BD),
        Hombre = E(0xFDF9F0, 0xF1EADB, 0x7C8CA0, 0x56677E, RH),
        Mujer = E(0xFDF9F0, 0xF4E6DE, 0xA86A6A, 0x8E3B46, RM),
        Otro = E(0xFDF9F0, 0xEFE7E6, 0x8C7A9C, 0x6E5A84, RO),
        SinNombre = E(0xFDF9F0, 0xEEE6D6, 0xAFA08A, 0x958670, RO),
        LineaArbol = C(0xA08A69), Pareja = C(0x8C7656), Directa = C(0x9C7420), Linaje = C(0x8A2E2E), BrilloLinaje = Color.FromArgb(0x26, 0x8A, 0x2E, 0x2E),
        Busqueda = C(0x5F8A12), Seleccion = C(0x2F2116), HaloSeleccion = Color.FromArgb(0x18, 0x2F, 0x21, 0x16),
        HaloDirecta = Color.FromArgb(0x2C, 0x9C, 0x74, 0x20), BrilloDirecta = Color.FromArgb(0x24, 0x9C, 0x74, 0x20),
        TintaNombre = C(0x2F2116), Apellidos = C(0x6E5A44), Fechas = C(0x86704F), GrosorBorde = 1.3, GrosorLinea = 1.8,
        LetraNombre = Estilo.Libro, LetraApellidos = Estilo.Libro, ApellidosCursiva = true,
    };

    public static readonly Tema Jardin = new()
    {
        Id = "jardin", Claro = true,
        Fondo = C(0xEAF1E4), Barra = C(0xDCE8D3), Panel = C(0xF8FBF5), Linea = C(0xCADAC0), Texto = C(0x1F2D22), Suave = C(0x56685A),
        Destacado = C(0x2F7A3A), SobreDestacado = C(0xFFFFFF),
        Boton = C(0x3E8E4A), BotonEncima = C(0x4AA057), BotonPulsado = C(0x347A3F), BotonTexto = C(0xFFFFFF),
        Secundario = C(0xDCE7D5), Aviso = C(0x1F2D22), Rojo = C(0xB3261E), Verde = C(0x2F7A3A),
        Lienzo1 = C(0xF2F7EE), Lienzo2 = C(0xDEEAD5),
        Hombre = E(0xFFFFFF, 0xE0EDF2, 0x6E9DB4, 0x2F7395, RH),
        Mujer = E(0xFFFFFF, 0xFCE6DC, 0xDE9A86, 0xC2614A, RM),
        Otro = E(0xFFFFFF, 0xECE7F4, 0x9A88BE, 0x6F5A9E, RO),
        SinNombre = E(0xFFFFFF, 0xECEFE9, 0xA0AA9C, 0x7F8A7B, RO),
        LineaArbol = C(0x9C8366), Pareja = C(0x8A7458), Directa = C(0xB8862B), Linaje = C(0x3E8E4A), BrilloLinaje = Color.FromArgb(0x30, 0x3E, 0x8E, 0x4A),
        Busqueda = C(0xD9722B), Seleccion = C(0x1F2D22), HaloSeleccion = Color.FromArgb(0x18, 0x1F, 0x2D, 0x22),
        HaloDirecta = Color.FromArgb(0x30, 0xD8, 0xA8, 0x40), BrilloDirecta = Color.FromArgb(0x28, 0xD8, 0xA8, 0x40),
        TintaNombre = C(0x1F2D22), Apellidos = C(0x4F6152), Fechas = C(0x66786A), GrosorBorde = 1.3, GrosorLinea = 2.6,
    };

    public static readonly Tema Contraste = new()
    {
        Id = "contraste", Claro = true,
        Fondo = C(0xFFFFFF), Barra = C(0xFFFFFF), Panel = C(0xFFFFFF), Linea = C(0x6A6A6A), Texto = C(0x000000), Suave = C(0x2B2B2B),
        Destacado = C(0x003FA3), SobreDestacado = C(0xFFFFFF),
        Boton = C(0x000000), BotonEncima = C(0x333333), BotonPulsado = C(0x000000), BotonTexto = C(0xFFE14D),
        Secundario = C(0xE6E6E6), Aviso = C(0x000000), Rojo = C(0xB00020), Verde = C(0x1B6E20),
        Lienzo1 = C(0xFFFFFF), Lienzo2 = C(0xFFFFFF),
        Hombre = E(0xFFFFFF, 0xFFFFFF, 0x003FA3, 0x003FA3, RH),
        Mujer = E(0xFFFFFF, 0xFFFFFF, 0xA3004F, 0xA3004F, RM),
        Otro = E(0xFFFFFF, 0xFFFFFF, 0x5A2DA0, 0x5A2DA0, RO),
        SinNombre = E(0xFFFFFF, 0xFFFFFF, 0x6A6A6A, 0x6A6A6A, RO),
        LineaArbol = C(0x444444), Pareja = C(0x444444), Directa = C(0x000000), Linaje = C(0x000000), BrilloLinaje = C(0xFFE14D),
        Busqueda = C(0xD35400), Seleccion = C(0x000000), HaloSeleccion = Color.FromArgb(0x30, 0x00, 0x00, 0x00),
        HaloDirecta = C(0xFFE14D), BrilloDirecta = null,
        TintaNombre = C(0x000000), Apellidos = C(0x1A1A1A), Fechas = C(0x2E2E2E),
        GrosorLinea = 2.4, GrosorBorde = 2.2, GrosorBordeDirecta = 3, PesoNombre = FontWeight.Bold,
    };

    public static readonly IReadOnlyList<Tema> Todos = new[] { Jardin, Dia, Album, Noche, Contraste };

    public static Tema Leer(string? id) => Todos.FirstOrDefault(t => t.Id == id) ?? Jardin;

    /// <summary>El de ahora (el que se eligió la última vez).</summary>
    public static Tema Actual { get; private set; } = Jardin;

    /// <summary>Al cambiar de tema (para que lo que ya está en la pantalla se vuelva a pintar).</summary>
    public static event Action? Cambiado;

    /// <summary>Lo pone en toda la aplicación: los colores de las vistas, los de los estilos y los de los controles del sistema.</summary>
    public static void Poner(Tema t)
    {
        Actual = t;
        Paleta.CambiarTema();
        Vistas.Ui.PonerColores(t);
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = t.Claro ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;
        Cambiado?.Invoke();
    }
}
