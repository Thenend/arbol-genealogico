using System.Globalization;
using ArbolGenealogico.Core.Model;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Core.Idiomas;

/// <summary>Las fechas escritas en el idioma de ahora: «3 de marzo de 1920», «March 3, 1920», «3. März 1920»…</summary>
public static class Fechas
{
    private static readonly Dictionary<string, string[]> NombresMes = new()
    {
        ["es"] = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" },
        ["en"] = new[] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" },
        ["fr"] = new[] { "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre" },
        ["it"] = new[] { "gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre" },
        ["de"] = new[] { "Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember" },
        ["pt"] = new[] { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" },
    };

    /// <summary>El nombre del mes (1 a 12), como va dentro de una fecha («marzo», «March»).</summary>
    public static string Mes(int mes) => NombresMes[Actual][mes - 1];

    /// <summary>El año, con «a. C.» (o «BC», «v. Chr.»…) si es de antes de Cristo.</summary>
    public static string Anio(int anio) => anio < 0 ? T("{0} a. C.", -anio) : anio.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Día, mes y año (el año ya escrito) en el orden y la forma de cada idioma: «3 de marzo de 1920», «March 3, 1920»,
    /// «1er mars 1920», «1º marzo 1920», «3. März 1920», «3 de março de 1920»; sin día, «marzo de 1920»; sin mes, el año.
    /// </summary>
    public static string Escribir(int? dia, int? mes, string anio)
    {
        var idioma = Actual;
        if (mes is not { } m) return anio;
        var nombre = Mes(m);
        if (dia is not { } d)
            return idioma is "es" or "pt" ? $"{nombre} de {anio}" : $"{nombre} {anio}";
        string dd = d == 1 ? idioma switch { "fr" => "1er", "it" or "pt" => "1º", _ => "1" } : d.ToString(CultureInfo.InvariantCulture);
        return idioma switch
        {
            "es" or "pt" => $"{dd} de {nombre} de {anio}",
            "en" => $"{nombre} {dd}, {anio}",
            "de" => $"{dd}. {nombre} {anio}",
            _ => $"{dd} {nombre} {anio}",
        };
    }

    /// <summary>La fecha entera: «3 de marzo de 1920», «hacia 1850», «15 de marzo de 44 a. C.».</summary>
    public static string Larga(Fecha f)
    {
        var s = Escribir(f.Dia, f.Mes, Anio(f.Anio));
        return f.Aproximada ? T("hacia {0}", s) : s;
    }

    /// <summary>Solo el año, para las tarjetas: «1920», «~1850», «44 a. C.».</summary>
    public static string Corta(Fecha f) => (f.Aproximada ? "~" : "") + Anio(f.Anio);

    /// <summary>
    /// Los años de una persona para su tarjeta: «1920 – 1998»; con solo uno, «* 1950» o «† 1890» (los signos de nacimiento
    /// y defunción de la genealogía, iguales en todos los idiomas). Si ha fallecido sin saberse cuándo, «1850 – ?» o, sin
    /// ninguna fecha, «†». Vacío si no hay nada que decir.
    /// </summary>
    public static string Anios(Persona p) => (p.Nacimiento, p.Defuncion) switch
    {
        ({ } n, { } d) => Corta(n) + " – " + Corta(d),
        ({ } n, null) => p.Fallecido ? Corta(n) + " – ?" : "* " + Corta(n),
        (null, { } d) => "† " + Corta(d),
        _ => p.Fallecido ? "†" : "",
    };

    /// <summary>
    /// Las dos fechas enteras y el lugar de nacimiento, para la ficha: «* 3 de marzo de 1920 en Ávila   † 1998» («†» solo,
    /// si no se sabe cuándo; «* Ávila», si solo se sabe dónde).
    /// </summary>
    public static string Vida(Persona p) => string.Join("   ", new[]
    {
        Nacio(p),
        p.Defuncion is { } d ? "† " + Larga(d) : p.Fallecido ? "†" : null,
    }.Where(x => x != null));

    private static string? Nacio(Persona p)
    {
        var lugar = p.LugarNacimiento?.Trim() ?? "";
        if (p.Nacimiento is { } n) return "* " + Larga(n) + (lugar.Length > 0 ? " " + T("en {0}", lugar) : "");
        return lugar.Length > 0 ? "* " + lugar : null;
    }
}
