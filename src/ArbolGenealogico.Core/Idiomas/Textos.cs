namespace ArbolGenealogico.Core.Idiomas;

/// <summary>
/// Las tablas de traducción: la clave es el texto en español tal como está en el código y el valor, su traducción. Los
/// huecos ({0}, {1}…) tienen que estar también en la traducción (las pruebas lo comprueban, y que no falte ninguna). Una
/// clave terminada en «|F» es la forma de mujer de un texto que cambia con el sexo de la persona.
/// </summary>
public static partial class Textos
{
    public static IReadOnlyDictionary<string, string> Tabla(string codigo) => codigo switch
    {
        "en" => En, "fr" => Fr, "it" => It, "de" => De, "pt" => Pt, _ => Es,
    };

    /// <summary>El español: el texto ya es la clave; aquí solo las formas de mujer.</summary>
    public static readonly Dictionary<string, string> Es = new()
    {
        ["Fue bautizado|F"] = "Fue bautizada",
        ["Fue adoptado|F"] = "Fue adoptada",
        ["Fue enterrado|F"] = "Fue enterrada",
        ["Fue incinerado|F"] = "Fue incinerada",
        ["Hermano de {0}|F"] = "Hermana de {0}",
        ["Hijo de {0}|F"] = "Hija de {0}",
        ["Padre de {0}|F"] = "Madre de {0}",
    };
}
