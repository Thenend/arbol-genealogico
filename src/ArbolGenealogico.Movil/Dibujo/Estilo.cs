using Avalonia.Media;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>Colores y forma de una tarjeta según el sexo (los mismos que en el escritorio).</summary>
public sealed class Estilo
{
    public required Color Fondo1, Fondo2, Borde, Acento;
    public required double Radio;

    public static readonly Color Oro = Color.FromRgb(0xF5, 0xC4, 0x51);
    public static readonly Color Linaje = Color.FromRgb(0x5E, 0xEA, 0xD4);
    public static readonly Color Lima = Color.FromRgb(0xA3, 0xE6, 0x35);
    public static readonly Color FondoApp = Color.FromRgb(0x0E, 0x10, 0x16);

    public static readonly Estilo Hombre = new()
    {
        Fondo1 = Color.FromRgb(0x24, 0x36, 0x5E), Fondo2 = Color.FromRgb(0x17, 0x22, 0x3F),
        Borde = Color.FromRgb(0x44, 0x6A, 0xBE), Acento = Color.FromRgb(0x72, 0xA2, 0xFF), Radio = 9,
    };
    public static readonly Estilo Mujer = new()
    {
        Fondo1 = Color.FromRgb(0x50, 0x25, 0x48), Fondo2 = Color.FromRgb(0x34, 0x19, 0x30),
        Borde = Color.FromRgb(0xB0, 0x52, 0x8A), Acento = Color.FromRgb(0xF5, 0x84, 0xC0), Radio = 26,
    };
    /// <summary>Sexo sin especificar: violeta, a medio camino entre el azul y el rosa (también en la forma).</summary>
    public static readonly Estilo Otro = new()
    {
        Fondo1 = Color.FromRgb(0x38, 0x2C, 0x5C), Fondo2 = Color.FromRgb(0x25, 0x1D, 0x3E),
        Borde = Color.FromRgb(0x7A, 0x5E, 0xC2), Acento = Color.FromRgb(0xB3, 0x9B, 0xFF), Radio = 16,
    };

    /// <summary>Por sexo; sin nombre, en gris (conserva la forma de su sexo).</summary>
    public static Estilo De(Sexo s, bool sinNombre)
    {
        var e = s switch { Sexo.Hombre => Hombre, Sexo.Mujer => Mujer, _ => Otro };
        if (!sinNombre) return e;
        return new Estilo
        {
            Fondo1 = Color.FromRgb(0x2D, 0x31, 0x3E), Fondo2 = Color.FromRgb(0x20, 0x23, 0x2D),
            Borde = Color.FromRgb(0x5A, 0x60, 0x72), Acento = Color.FromRgb(0xA3, 0xAA, 0xBE), Radio = e.Radio,
        };
    }

    public static Color ConAlfa(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    /// <summary>La letra de las tarjetas: grande, estrecha y muy legible (como la Bahnschrift del escritorio).</summary>
    public static readonly FontFamily Letra = new("avares://ArbolGenealogico.Movil/Recursos/Fuentes#Barlow Semi Condensed");
}
