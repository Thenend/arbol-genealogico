using Avalonia.Media;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// Los colores de la aplicación y del árbol en la pantalla: Jardín (verdes, con las líneas como ramas; el de la primera vez,
/// salvo en la variante con Álbum),
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
        "dia" => T("Día"), "album" => T("Álbum"), "tinta" => T("Papel y tinta"), "jardin" => T("Jardín"), "contraste" => T("Alto contraste"),
        "clasico" => "Clásico", "acuarela" => "Acuarela", "navidad" => "Navidad", "sencillo" => "Sencillo", "gala" => "Noche dorada", _ => T("Noche"),
    };
    /// <summary>Cómo es, en una línea.</summary>
    public string Detalle => Id switch
    {
        "dia" => T("Claro, con tarjetas blancas"),
        "album" => T("Sepia, como un álbum de fotos antiguo"),
        "tinta" => T("Dibujado a mano con plumilla, sobre papel"),
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
    /// <summary>Cuánto tiemblan los contornos de las tarjetas, como dibujados a mano (0: rectos; ver <see cref="AMano"/>).</summary>
    public double AMano { get; init; }
    /// <summary>Y cuánto las líneas del árbol (más poco: muchas juntas cansan la vista).</summary>
    public double AManoLineas { get; init; }
    /// <summary>El nudo de las parejas es un lacito de este color (null: el circulito de siempre).</summary>
    public Color? Lazo { get; init; }
    /// <summary>La letra de las tarjetas mientras no se elige otra (ver <see cref="Dibujo.Letra"/>).</summary>
    public string Letra { get; init; } = "atkinson";
    /// <summary>El grueso del nombre (como mínimo; si la letra lo pide, más).</summary>
    public FontWeight PesoNombre { get; init; } = FontWeight.SemiBold;

    // ---------- Prueba de estilos completos (propuesta) ----------

    /// <summary>Sin la franja del color del sexo (el sexo se ve en la forma y en el aro de la foto).</summary>
    public bool SinFranja { get; init; }
    /// <summary>Un segundo contorno, fino, por dentro del primero (null: ninguno).</summary>
    public Color? Doble { get; init; }
    /// <summary>Las tarjetas como manchas de acuarela del color de cada uno, sin contorno de tinta.</summary>
    public bool Acuarela { get; init; }
    /// <summary>El radio de las curvas de las líneas (0: el de siempre).</summary>
    public double RadioLineas { get; init; }
    /// <summary>Hojitas a lo largo de las líneas, de este color (null: ninguna).</summary>
    public Color? Hojas { get; init; }
    /// <summary>El nudo de las parejas: «circulo», «lazo», «rombo», «acebo» o «corazon».</summary>
    public string Nudo { get; init; } = "circulo";
    public Color? ColorNudo { get; init; }
    /// <summary>Grano de papel en el fondo.</summary>
    public bool Grano { get; init; }
    /// <summary>Sin sombra debajo de las tarjetas.</summary>
    public bool SinSombra { get; init; }

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
        Letra = "lora",
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


    // como el icono: papel crema, tarjetas de colores suaves con contorno de tinta y las líneas a plumilla
    public static readonly Tema Tinta = new()
    {
        Id = "tinta", Claro = true,
        Fondo = C(0xF2E8D6), Barra = C(0xE9DCC4), Panel = C(0xFAF4E8), Linea = C(0xD9C9AC), Texto = C(0x2B2622), Suave = C(0x6E6253),
        Destacado = C(0xA8402F), SobreDestacado = C(0xFCF8EE),
        Boton = C(0x2B2622), BotonEncima = C(0x403832), BotonPulsado = C(0x1A1714), BotonTexto = C(0xF8F0E2),
        Secundario = C(0xE8DCC6), Aviso = C(0x2B2622), Rojo = C(0xA8402F), Verde = C(0x58743F),
        Lienzo1 = C(0xF5ECDC), Lienzo2 = C(0xE9DCC4),
        // las tarjetas, crema como en Álbum; el color del sexo, en la franja y en la foto
        Hombre = E(0xFDF9F0, 0xF1EADB, 0x2B2622, 0x56677E, RH),
        Mujer = E(0xFDF9F0, 0xF4E6DE, 0x2B2622, 0x8E3B46, RM),
        Otro = E(0xFDF9F0, 0xEFE7E6, 0x2B2622, 0x6E5A84, RO),
        SinNombre = E(0xFDF9F0, 0xEEE6D6, 0x6E6253, 0x958670, RO),
        LineaArbol = C(0x3A332D), Pareja = C(0x3A332D), Directa = C(0xB07A1E), Linaje = C(0xA8402F), BrilloLinaje = Color.FromArgb(0x26, 0xA8, 0x40, 0x2F),
        Busqueda = C(0x58743F), Seleccion = C(0x1A1714), HaloSeleccion = Color.FromArgb(0x1C, 0x2B, 0x26, 0x22),
        HaloDirecta = Color.FromArgb(0x2C, 0xD0, 0x9A, 0x3A), BrilloDirecta = null,
        TintaNombre = C(0x2B2622), Apellidos = C(0x5A4E42), Fechas = C(0x6E6253), GrosorBorde = 3.1, GrosorBordeDirecta = 4.4, GrosorLinea = 3.7,
        Letra = "lora", AMano = 1.8, AManoLineas = 0.8,
        Lazo = C(0xC0463A), Nudo = "lazo", Grano = true,
    };

    // ---------- Estilos de prueba para la propuesta ----------

    // Clásico: marfil, doble filete verde y oro, letra Cormorant, nudo de rombo dorado (como la lámina Clásico)
    public static readonly Tema Clasico = new()
    {
        Id = "clasico", Claro = true,
        Fondo = C(0xF6F1E4), Barra = C(0xEDE5D0), Panel = C(0xFBF8F0), Linea = C(0xDCD2BA), Texto = C(0x24342A), Suave = C(0x5F6A5E),
        Destacado = C(0x2F4A35), SobreDestacado = C(0xFBF7EE),
        Boton = C(0x2F4A35), BotonEncima = C(0x3B5A42), BotonPulsado = C(0x243A29), BotonTexto = C(0xFBF7EE),
        Secundario = C(0xEAE2CC), Aviso = C(0x24342A), Rojo = C(0x9A2E24), Verde = C(0x2F4A35),
        Lienzo1 = C(0xFBF7EE), Lienzo2 = C(0xEFE6CF),
        Hombre = E(0xFFFDF7, 0xF8F3E6, 0x2F4A35, 0x3E5F7A, 3),
        Mujer = E(0xFFFDF7, 0xF8F1E8, 0x2F4A35, 0x8A3E4E, 16),
        Otro = E(0xFFFDF7, 0xF6F1EC, 0x2F4A35, 0x5E4E7A, 9),
        SinNombre = E(0xFFFDF7, 0xF3EFE4, 0x8A8A78, 0x8A8A78, 9),
        LineaArbol = C(0x2F4A35), Pareja = C(0x2F4A35), Directa = C(0xB8913A), Linaje = C(0x8A3E4E), BrilloLinaje = Color.FromArgb(0x26, 0x8A, 0x3E, 0x4E),
        Busqueda = C(0x5F8A12), Seleccion = C(0x1A241C), HaloSeleccion = Color.FromArgb(0x18, 0x1A, 0x24, 0x1C),
        HaloDirecta = Color.FromArgb(0x00, 0, 0, 0), BrilloDirecta = null,
        TintaNombre = C(0x1E2A22), Apellidos = C(0x4E5A50), Fechas = C(0x6A6A58),
        GrosorBorde = 1.6, GrosorBordeDirecta = 2.2, GrosorLinea = 1.7,
        Letra = "cormorant", SinFranja = true, Doble = C(0xB8913A), Nudo = "rombo", ColorNudo = C(0xB8913A), RadioLineas = 4, SinSombra = true,
    };

    // Acuarela: papel de flores, tarjetas como manchas de acuarela, ramas con hojitas y un corazón en las parejas (como Floral)
    public static readonly Tema Acuarela_ = new()
    {
        Id = "acuarela", Claro = true,
        Fondo = C(0xFBF6EF), Barra = C(0xF1E9DD), Panel = C(0xFFFCF7), Linea = C(0xE2D6C6), Texto = C(0x3A3A32), Suave = C(0x6E6A5E),
        Destacado = C(0xA0584E), SobreDestacado = C(0xFFFBF6),
        Boton = C(0x5D7A52), BotonEncima = C(0x6C8C60), BotonPulsado = C(0x4C6643), BotonTexto = C(0xFFFFFF),
        Secundario = C(0xEFE6DA), Aviso = C(0x3A3A32), Rojo = C(0xA0584E), Verde = C(0x5D7A52),
        Lienzo1 = C(0xFFFCF7), Lienzo2 = C(0xF6EEE2),
        Hombre = E(0xFFFFFF, 0xFFFFFF, 0x7FA3B5, 0x6E95AA, 14),
        Mujer = E(0xFFFFFF, 0xFFFFFF, 0xE09A8C, 0xD98A7E, 26),
        Otro = E(0xFFFFFF, 0xFFFFFF, 0xB0A0CC, 0x9E8CC0, 20),
        SinNombre = E(0xFFFFFF, 0xFFFFFF, 0xC4BFB2, 0xB4AE9E, 20),
        LineaArbol = C(0x8A6E4E), Pareja = C(0x8A6E4E), Directa = C(0x8A6E4E), Linaje = C(0xA0584E), BrilloLinaje = Color.FromArgb(0x26, 0xA0, 0x58, 0x4E),
        Busqueda = C(0xD9722B), Seleccion = C(0x3A3A32), HaloSeleccion = Color.FromArgb(0x18, 0x3A, 0x3A, 0x32),
        HaloDirecta = Color.FromArgb(0x00, 0, 0, 0), BrilloDirecta = null,
        TintaNombre = C(0x2E3A2C), Apellidos = C(0x5E5A4E), Fechas = C(0x7A7262),
        GrosorBorde = 0, GrosorBordeDirecta = 0, GrosorLinea = 2.6,
        Letra = "playfair", SinFranja = true, Acuarela = true, RadioLineas = 40, Hojas = C(0x7C9A62), Nudo = "corazon", ColorNudo = C(0xD27A6E),
        SinSombra = true, AManoLineas = 0.5,
    };

    // Navidad: tarjetas blancas con filete rojo y oro, líneas verde abeto y acebo en las parejas (como la lámina Navidad)
    public static readonly Tema Navidad = new()
    {
        Id = "navidad", Claro = true,
        Fondo = C(0xF7F1E6), Barra = C(0xEDE3D2), Panel = C(0xFCF8F0), Linea = C(0xDCCFB8), Texto = C(0x1F3A2A), Suave = C(0x5A6A5E),
        Destacado = C(0xA8322D), SobreDestacado = C(0xFFFFFF),
        Boton = C(0xA8322D), BotonEncima = C(0xBC3E38), BotonPulsado = C(0x8E2824), BotonTexto = C(0xFFFFFF),
        Secundario = C(0xEDE3D2), Aviso = C(0x1F3A2A), Rojo = C(0xA8322D), Verde = C(0x2E5A3A),
        Lienzo1 = C(0xFCF8F0), Lienzo2 = C(0xF0E6D4),
        Hombre = E(0xFFFFFF, 0xFAF6EE, 0x2E5A3A, 0x2E5A3A, 9),
        Mujer = E(0xFFFFFF, 0xFBF3EE, 0xA8322D, 0xA8322D, 26),
        Otro = E(0xFFFFFF, 0xF8F4F0, 0x6E5A84, 0x6E5A84, 16),
        SinNombre = E(0xFFFFFF, 0xF6F2EA, 0xA8A090, 0xA8A090, 16),
        LineaArbol = C(0x2E5A3A), Pareja = C(0x2E5A3A), Directa = C(0xB8913A), Linaje = C(0xA8322D), BrilloLinaje = Color.FromArgb(0x26, 0xA8, 0x32, 0x2D),
        Busqueda = C(0xD9722B), Seleccion = C(0x1F3A2A), HaloSeleccion = Color.FromArgb(0x18, 0x1F, 0x3A, 0x2A),
        HaloDirecta = Color.FromArgb(0x22, 0xD8, 0xB0, 0x50), BrilloDirecta = null,
        TintaNombre = C(0x1F3A2A), Apellidos = C(0x4E5E52), Fechas = C(0x6A6A5A),
        GrosorBorde = 2, GrosorBordeDirecta = 2.6, GrosorLinea = 2.4,
        Letra = "playfair", SinFranja = true, Doble = C(0xC9A24A), Nudo = "acebo", RadioLineas = 10, SinSombra = true,
    };

    // Sencillo: blanco roto, tarjetas sin contorno con sombra suave, líneas finas muy curvas (como la lámina Sencillo)
    public static readonly Tema Sencillo = new()
    {
        Id = "sencillo", Claro = true,
        Fondo = C(0xF5F4F0), Barra = C(0xFFFFFF), Panel = C(0xFFFFFF), Linea = C(0xE2E0D8), Texto = C(0x26302A), Suave = C(0x66706A),
        Destacado = C(0x4E7A5E), SobreDestacado = C(0xFFFFFF),
        Boton = C(0x26302A), BotonEncima = C(0x38443C), BotonPulsado = C(0x1A211D), BotonTexto = C(0xFFFFFF),
        Secundario = C(0xEAE8E2), Aviso = C(0x26302A), Rojo = C(0xB3261E), Verde = C(0x4E7A5E),
        Lienzo1 = C(0xFAF9F6), Lienzo2 = C(0xF1EFEA),
        Hombre = E(0xFFFFFF, 0xFFFFFF, 0xFFFFFF, 0x7C9CB0, 12),
        Mujer = E(0xFFFFFF, 0xFFFFFF, 0xFFFFFF, 0xC99486, 26),
        Otro = E(0xFFFFFF, 0xFFFFFF, 0xFFFFFF, 0x9C90B8, 18),
        SinNombre = E(0xFFFFFF, 0xFFFFFF, 0xFFFFFF, 0xB0B0A8, 18),
        LineaArbol = C(0x9AA69E), Pareja = C(0x9AA69E), Directa = C(0x4E7A5E), Linaje = C(0x4E7A5E), BrilloLinaje = Color.FromArgb(0x26, 0x4E, 0x7A, 0x5E),
        Busqueda = C(0xD9722B), Seleccion = C(0x26302A), HaloSeleccion = Color.FromArgb(0x18, 0x26, 0x30, 0x2A),
        HaloDirecta = Color.FromArgb(0x00, 0, 0, 0), BrilloDirecta = null,
        TintaNombre = C(0x26302A), Apellidos = C(0x66706A), Fechas = C(0x86908A),
        GrosorBorde = 0.01, GrosorBordeDirecta = 1.6, GrosorLinea = 1.5,
        Letra = "nunito", SinFranja = true, RadioLineas = 30,
    };

    // Noche dorada: azul noche con filetes de oro (para ver en pantalla y compartir como imagen)
    public static readonly Tema Gala = new()
    {
        Id = "gala", Claro = false,
        Fondo = C(0x10162A), Barra = C(0x161E36), Panel = C(0x1A2340), Linea = C(0x2A355A), Texto = C(0xF1EAD8), Suave = C(0xA9A38E),
        Destacado = C(0xD9B75E), SobreDestacado = C(0x10162A),
        Boton = C(0xD9B75E), BotonEncima = C(0xE6C676), BotonPulsado = C(0xC2A04A), BotonTexto = C(0x10162A),
        Secundario = C(0x222C4C), Aviso = C(0x2A355A), Rojo = C(0xFF7A7A), Verde = C(0x7BD88F),
        Lienzo1 = C(0x18213D), Lienzo2 = C(0x0B1020),
        Hombre = E(0x1C2646, 0x161E38, 0xC9A85A, 0x8FB0E0, 3),
        Mujer = E(0x1C2646, 0x161E38, 0xC9A85A, 0xE7A4BC, 16),
        Otro = E(0x1C2646, 0x161E38, 0xC9A85A, 0xB9A4E8, 9),
        SinNombre = E(0x1C2646, 0x161E38, 0x6A6A6A, 0x8A8A8A, 9),
        LineaArbol = C(0xB89A55), Pareja = C(0xB89A55), Directa = C(0xE8C670), Linaje = C(0x8FD6C6), BrilloLinaje = Color.FromArgb(0x40, 0x8F, 0xD6, 0xC6),
        Busqueda = C(0xA3E635), Seleccion = Colors.White, HaloSeleccion = Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF),
        HaloDirecta = Color.FromArgb(0x00, 0, 0, 0), BrilloDirecta = Color.FromArgb(0x30, 0xE8, 0xC6, 0x70),
        TintaNombre = C(0xF6EFDC), Apellidos = C(0xC9C0A6), Fechas = C(0xA9A38E),
        GrosorBorde = 1.3, GrosorBordeDirecta = 2, GrosorLinea = 1.5,
        Letra = "cormorant", SinFranja = true, Doble = C(0x6E5E36), Nudo = "rombo", ColorNudo = C(0xE8C670), RadioLineas = 4, SinSombra = true,
    };

    // la variante con Álbum (compilada con -p:TemaAlbum=true, para comparar las dos en la tienda) empieza con Álbum y lo pone
    // el primero de la lista; la de siempre, con Jardín
#if TEMA_ALBUM
    public static readonly IReadOnlyList<Tema> Todos = new[] { Album, Jardin, Tinta, Dia, Noche, Contraste };
#else
    public static readonly IReadOnlyList<Tema> Todos = new[] { Jardin, Dia, Album, Tinta, Noche, Contraste, Clasico, Acuarela_, Navidad, Sencillo, Gala };
#endif

    /// <summary>El de la primera vez: el primero de la lista.</summary>
    public static Tema PorDefecto => Todos[0];

    public static Tema Leer(string? id) => Todos.FirstOrDefault(t => t.Id == id) ?? PorDefecto;

    /// <summary>El de ahora (el que se eligió la última vez).</summary>
    public static Tema Actual { get; private set; } = PorDefecto;

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

    /// <summary>Vuelve a pintar el árbol con los mismos colores (p. ej. al cambiar de letra).</summary>
    public static void Repintar()
    {
        Paleta.CambiarTema();
        Cambiado?.Invoke();
    }
}
