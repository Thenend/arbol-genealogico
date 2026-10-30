using Avalonia.Media;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// La letra de las tarjetas del árbol (en la pantalla, en el PDF, en las hojas y en la lámina), aparte de los colores:
/// mientras no se elige una, la del tema (Lora en Álbum, Atkinson en los demás). Todas son libres (OFL) y llevan bien las
/// tildes; se sacaron de los TTF variables de google/fonts con un peso fijo y solo con las letras latinas. A Caveat se le
/// apretó el alto de línea (820 y 210 en vez de 960 y 300), para que en la tarjeta quepa más grande.
/// </summary>
public sealed class Letra
{
    /// <summary>Lo que se guarda en las preferencias («barlow», «lora»…).</summary>
    public required string Id { get; init; }
    /// <summary>El nombre de la fuente (no se traduce).</summary>
    public required string Nombre { get; init; }
    public required FontFamily Familia { get; init; }
    public FontWeight PesoNombre { get; init; } = FontWeight.SemiBold;
    public FontWeight PesoApellidos { get; init; } = FontWeight.Medium;
    /// <summary>Los apellidos y los años en cursiva (en Lora, como en un álbum).</summary>
    public bool ApellidosCursiva { get; init; }
    /// <summary>Cuánto más grande que Barlow, para que se lea igual (las letras a mano son más pequeñas).</summary>
    public double Escala { get; init; } = 1;
    /// <summary>Otra letra para los apellidos y los años (null: la misma).</summary>
    public FontFamily? FamiliaApellidos { get; init; }
    /// <summary>La escala de los apellidos y los años, si llevan otra letra (null: la misma que el nombre).</summary>
    public double? EscalaApellidos { get; init; }
    /// <summary>El nombre también en cursiva (letras que solo tienen cursiva).</summary>
    public bool Cursiva { get; init; }

    /// <summary>Cómo es, en una línea.</summary>
    public string Detalle => Id switch
    {
        "lora" => T("De libro, con los apellidos en cursiva"),
        "atkinson" => T("Muy clara, hecha para quien ve poco"),
        "nunito" => T("Redondeada y amable"),
        "merriweather" => T("De libro, robusta: la que mejor se lee en papel"),
        "caveat" => T("Como escrita a mano"),
        _ => T("Estrecha: caben los nombres largos"),
    };

    private static FontFamily F(string nombre) => new("avares://ArbolGenealogico.Movil/Recursos/Fuentes#" + nombre);

    public static readonly Letra Barlow = new() { Id = "barlow", Nombre = "Barlow", Familia = Estilo.Letra };
    public static readonly Letra Lora = new() { Id = "lora", Nombre = "Lora", Familia = Estilo.Libro, ApellidosCursiva = true };
    public static readonly Letra Atkinson = new() { Id = "atkinson", Nombre = "Atkinson", Familia = F("Atkinson Hyperlegible Next") };
    // fina: un punto más gruesa que las demás
    public static readonly Letra Nunito = new()
    {
        Id = "nunito", Nombre = "Nunito", Familia = F("Nunito"), PesoNombre = FontWeight.Bold, PesoApellidos = FontWeight.SemiBold,
    };
    public static readonly Letra Merriweather = new() { Id = "merriweather", Nombre = "Merriweather", Familia = F("Merriweather") };
    public static readonly Letra Caveat = new()
    {
        Id = "caveat", Nombre = "Caveat", Familia = F("Caveat"), PesoNombre = FontWeight.Bold, PesoApellidos = FontWeight.Bold, Escala = 1.2,
    };

    // de prueba, para los estilos completos (las de las láminas)
    public static readonly Letra Cormorant = new() { Id = "cormorant", Nombre = "Cormorant", Familia = F("Cormorant Garamond"), ApellidosCursiva = true, Escala = 1.12 };
    public static readonly Letra Playfair = new() { Id = "playfair", Nombre = "Playfair", Familia = F("Playfair Display"), ApellidosCursiva = true };

    // de prueba, manuscritas antiguas para el estilo Pergamino (los apellidos, en IM Fell cursiva, que se lee mejor)
    public static readonly Letra Petit = new()
    {
        Id = "petit", Nombre = "Petit Formal", Familia = F("Petit Formal Script"), PesoNombre = FontWeight.Normal, PesoApellidos = FontWeight.Normal,
        FamiliaApellidos = F("IM FELL English"), ApellidosCursiva = true, Escala = 1.0, EscalaApellidos = 1.05,
    };
    public static readonly Letra Italianno = new()
    {
        Id = "italianno", Nombre = "Italianno", Familia = F("Italianno"), PesoNombre = FontWeight.Normal, PesoApellidos = FontWeight.Normal,
        FamiliaApellidos = F("IM FELL English"), ApellidosCursiva = true, Escala = 1.7, EscalaApellidos = 1.05,
    };
    public static readonly Letra Fell = new()
    {
        Id = "fell", Nombre = "IM Fell", Familia = F("IM FELL English"), PesoNombre = FontWeight.Normal, PesoApellidos = FontWeight.Normal,
        ApellidosCursiva = true, Cursiva = true, Escala = 1.1,
    };

    public static readonly IReadOnlyList<Letra> Todas = new[] { Atkinson, Lora, Barlow, Nunito, Merriweather, Caveat, Cormorant, Playfair, Petit, Italianno, Fell };

    /// <summary>La de ese id; null si no se eligió ninguna (o ya no existe): la del tema.</summary>
    public static Letra? Leer(string? id) => Todas.FirstOrDefault(l => l.Id == id);

    /// <summary>La que se eligió (null: la del tema).</summary>
    public static Letra? Elegida { get; private set; }

    /// <summary>La de ahora: la elegida o, si no, la del tema.</summary>
    public static Letra Actual => Elegida ?? Leer(Tema.Actual.Letra) ?? Atkinson;

    /// <summary>La pone en el árbol (y en lo que se imprime), sin tocar los colores.</summary>
    public static void Poner(Letra? l)
    {
        Elegida = l;
        Tema.Repintar();
    }
}
