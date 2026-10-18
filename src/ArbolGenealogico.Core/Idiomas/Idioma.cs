using System.Globalization;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Idiomas;

/// <summary>
/// Los idiomas de la aplicación del móvil. Los textos se escriben en español en el código y ese mismo texto es la clave
/// de su traducción (como en gettext): <c>T("Nuevo árbol")</c> da «New tree» en inglés. Cada idioma tiene su tabla
/// (<see cref="Textos"/>, en Ingles.cs, Frances.cs…); lo que no esté en ella sale en español. Un idioma que no se tiene
/// (el del teléfono es, p. ej., neerlandés) sale en inglés. El escritorio y la web no lo eligen: siguen en español.
/// </summary>
public static class Idioma
{
    /// <summary>Los idiomas que hay, el primero el original.</summary>
    public static readonly string[] Codigos = { "es", "en", "fr", "it", "de", "pt" };

    private sealed record Estado(string Codigo, CultureInfo Cultura, IReadOnlyDictionary<string, string> Tabla);

    private static Estado _global = Crear("es");
    // para las pruebas, que van en paralelo: un idioma solo para el código que lo pide (y lo que llame), sin tocar el de todos
    private static readonly AsyncLocal<Estado?> _local = new();
    private static Estado Ahora => _local.Value ?? _global;

    /// <summary>El idioma de ahora: «es», «en», «fr», «it», «de» o «pt».</summary>
    public static string Actual => Ahora.Codigo;

    /// <summary>La cultura del idioma de ahora (para fechas y números).</summary>
    public static CultureInfo Cultura => Ahora.Cultura;

    /// <summary>Usa el idioma de esa cultura («pt-BR», «fr_CA», «nl»…): uno de los que hay, o inglés. Devuelve el código.</summary>
    public static string Elegir(string? cultura)
    {
        _global = Crear(Codigo(cultura));
        return _global.Codigo;
    }

    /// <summary>Usa ese idioma solo hasta que se deseche lo devuelto, y solo aquí (para las pruebas).</summary>
    public static IDisposable Probar(string cultura)
    {
        var antes = _local.Value;
        _local.Value = Crear(Codigo(cultura));
        return new Deshacer(() => _local.Value = antes);
    }

    private sealed class Deshacer(Action accion) : IDisposable
    {
        public void Dispose() => accion();
    }

    public static string Codigo(string? cultura)
    {
        var c = (cultura ?? "").Replace('_', '-').Split('-')[0].Trim().ToLowerInvariant();
        return Codigos.Contains(c) ? c : "en";
    }

    private static Estado Crear(string codigo) => new(codigo, CultureInfo.GetCultureInfo(codigo switch
    {
        "es" => "es-ES", "en" => "en-US", "fr" => "fr-FR", "it" => "it-IT", "de" => "de-DE", _ => "pt-BR",
    }), Textos.Tabla(codigo));

    /// <summary>El texto en el idioma de ahora.</summary>
    public static string T(string es) => Ahora.Tabla.TryGetValue(es, out var t) ? t : es;

    /// <summary>El texto en el idioma de ahora, con los huecos ({0}, {1}…) rellenos.</summary>
    public static string T(string es, params object?[] datos) => string.Format(Cultura, T(es), datos);

    /// <summary>
    /// Un texto que cambia según el sexo de la persona («Fue bautizado» / «Fue bautizada», «Né» / «Née»): la forma de
    /// mujer está en la tabla con la clave terminada en «|F». Sin sexo conocido, la otra.
    /// </summary>
    public static string T(string es, Sexo sexo) =>
        sexo == Sexo.Mujer && Ahora.Tabla.TryGetValue(es + "|F", out var f) ? f : T(es);

    /// <summary>Como <see cref="T(string, Sexo)"/>, con los huecos rellenos.</summary>
    public static string T(string es, Sexo sexo, params object?[] datos) => string.Format(Cultura, T(es, sexo), datos);

    /// <summary>Singular o plural según <paramref name="n"/> («1 persona», «3 personas»), con el número en {0}.</summary>
    public static string N(int n, string uno, string varios) =>
        T(n == 1 ? uno : varios, n);
}
