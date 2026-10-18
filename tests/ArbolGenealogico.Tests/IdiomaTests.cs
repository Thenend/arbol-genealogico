using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArbolGenealogico.Core.Idiomas;
using ArbolGenealogico.Core.Io;

namespace ArbolGenealogico.Tests;

public class IdiomaTests
{
    /// <summary>Los nombres propios del árbol de ejemplo, que son iguales en todos los idiomas.</summary>
    private static readonly HashSet<string> NombresPropios = new() { "Rosa", "Ramón", "Carmen", "Álvaro", "Inés", "Silvia", "Pablo", "Teodoro", "Pilar" };

    private static string Raiz()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ArbolGenealogico.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("No se encuentra el proyecto.");
    }

    /// <summary>Los archivos de las tablas de traducción (sus claves no son textos del código).</summary>
    private static readonly HashSet<string> Tablas = new() { "Textos.cs", "Ingles.cs", "Frances.cs", "Italiano.cs", "Aleman.cs", "Portugues.cs" };

    private const string Literal = @"""(?:[^""\\\n]|\\.)*""";
    private static readonly string Cadena = Literal + @"(?:\s*\+\s*" + Literal + ")*";

    private static string Unir(string cadena) => string.Concat(Regex.Matches(cadena, Literal).Select(m => m.Value[1..^1]))
        .Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

    /// <summary>Todos los textos que se traducen: los de T("…") y N(n, "…", "…") del código y los del árbol de ejemplo.</summary>
    private static HashSet<string> Claves()
    {
        var raiz = Raiz();
        var claves = new HashSet<string>();
        foreach (var carpeta in new[] { "ArbolGenealogico.Movil", "ArbolGenealogico.Movil.Android", "ArbolGenealogico.Core" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(raiz, "src", carpeta), "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || Tablas.Contains(Path.GetFileName(f))) continue;
                var texto = File.ReadAllText(f);
                foreach (Match m in Regex.Matches(texto, @"\bT\(\s*(" + Cadena + ")")) claves.Add(Unir(m.Groups[1].Value));
                foreach (Match m in Regex.Matches(texto, @"\bN\([^,()]+(?:\([^()]*\))?[^,()]*,\s*(" + Cadena + @")\s*,\s*(" + Cadena + ")"))
                {
                    claves.Add(Unir(m.Groups[1].Value));
                    claves.Add(Unir(m.Groups[2].Value));
                }
                if (f.EndsWith("Gedcom.cs"))
                    foreach (Match m in Regex.Matches(texto, @"(?:Evento|ConFecha)\(""[A-Z]+"", (" + Literal + ")")) claves.Add(Unir(m.Groups[1].Value));
            }
        foreach (var ejemplo in new[] { "familia-garcia.ramo", "familia-perez.ramo" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(raiz, "Ejemplos", ejemplo)));
            claves.Add(doc.RootElement.GetProperty("nombre").GetString()!);
            foreach (var p in doc.RootElement.GetProperty("personas").EnumerateArray())
            {
                if (p.GetProperty("nombre").GetString() is { Length: > 0 } n && !NombresPropios.Contains(n)) claves.Add(n);
                if (p.TryGetProperty("historia", out var h) && h.GetString() is { Length: > 0 } hs) claves.Add(hs);
            }
        }
        return claves;
    }

    private static string Huecos(string s) => string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().OrderBy(x => x));

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("pt")]
    public void Cada_idioma_tiene_todos_los_textos_y_ninguno_de_sobra(string idioma)
    {
        var claves = Claves();
        Assert.True(claves.Count > 250, $"Solo {claves.Count} claves: ¿ha cambiado la forma de escribirlas?");
        var tabla = Textos.Tabla(idioma);
        var faltan = claves.Where(c => !tabla.ContainsKey(c)).ToList();
        Assert.True(faltan.Count == 0, $"Faltan en «{idioma}»:\n" + string.Join("\n", faltan));
        var sobran = tabla.Keys.Where(k => !claves.Contains(k.EndsWith("|F") ? k[..^2] : k)).ToList();
        Assert.True(sobran.Count == 0, $"Sobran en «{idioma}» (ya no están en el código):\n" + string.Join("\n", sobran));
        var malos = tabla.Where(kv => Huecos(kv.Key.EndsWith("|F") ? kv.Key[..^2] : kv.Key) != Huecos(kv.Value)).Select(kv => kv.Key).ToList();
        Assert.True(malos.Count == 0, $"Huecos distintos en «{idioma}»:\n" + string.Join("\n", malos));
        Assert.All(tabla.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Fact]
    public void Las_formas_de_mujer_del_espanol_son_de_textos_que_existen()
    {
        var claves = Claves();
        Assert.All(Textos.Es.Keys, k => Assert.Contains(k[..^2], claves));
    }

    [Theory]
    [InlineData("es-ES", "es")]
    [InlineData("pt-BR", "pt")]
    [InlineData("pt_PT", "pt")]
    [InlineData("fr-CA", "fr")]
    [InlineData("de", "de")]
    [InlineData("nl-NL", "en")]
    [InlineData("", "en")]
    public void Elige_el_idioma_de_la_cultura_o_el_ingles(string cultura, string codigo) => Assert.Equal(codigo, Idioma.Codigo(cultura));

    [Fact]
    public void Plurales_y_formas_de_mujer()
    {
        using (Idioma.Probar("en"))
        {
            Assert.Equal("1 person", Idioma.N(1, "1 persona", "{0} personas"));
            Assert.Equal("0 people", Idioma.N(0, "1 persona", "{0} personas"));
            Assert.Equal("12 people", Idioma.N(12, "1 persona", "{0} personas"));
        }
        using (Idioma.Probar("fr"))
        {
            Assert.Equal("Née", Idioma.T("Nació", Core.Model.Sexo.Mujer));
            Assert.Equal("Né", Idioma.T("Nació", Core.Model.Sexo.Hombre));
        }
        Assert.Equal("Fue bautizada", Idioma.T("Fue bautizado", Core.Model.Sexo.Mujer));
        Assert.Equal("Nació", Idioma.T("Nació", Core.Model.Sexo.Mujer));
    }

    [Theory]
    [InlineData("en", "3 MAR 1920", "on March 3, 1920")]
    [InlineData("en", "MAR 1920", "in March 1920")]
    [InlineData("en", "ABT 1900", "about 1900")]
    [InlineData("en", "BET 1921 AND 1922", "between 1921 and 1922")]
    [InlineData("fr", "1 MAR 1920", "le 1er mars 1920")]
    [InlineData("fr", "1920", "en 1920")]
    [InlineData("fr", "BEF 12 JAN 1900", "avant le 12 janvier 1900")]
    [InlineData("fr", "FROM 1900 TO 1905", "de 1900 à 1905")]
    [InlineData("it", "3 MAR 1920", "il 3 marzo 1920")]
    [InlineData("it", "8 MAR 1920", "l'8 marzo 1920")]
    [InlineData("it", "1920", "nel 1920")]
    [InlineData("it", "APR 1920", "nell'aprile 1920")]
    [InlineData("it", "FROM 1900 TO 1905", "dal 1900 al 1905")]
    [InlineData("it", "AFT 1880", "dopo il 1880")]
    [InlineData("it", "AFT 800", "dopo l'800")]
    [InlineData("de", "3 MAR 1920", "am 3. März 1920")]
    [InlineData("de", "MAR 1920", "im März 1920")]
    [InlineData("de", "1920", "1920")]
    [InlineData("de", "BEF 12 JAN 1900", "vor dem 12. Januar 1900")]
    [InlineData("pt", "1 MAR 1920", "em 1º de março de 1920")]
    [InlineData("pt", "ABT 1900", "por volta de 1900")]
    [InlineData("pt", "(na guerra)", "(na guerra)")]
    public void Fechas_en_cada_idioma(string idioma, string gedcom, string esperado)
    {
        using var _ = Idioma.Probar(idioma);
        Assert.Equal(esperado, Gedcom.FraseFecha(gedcom));
    }

    [Fact]
    public void Un_gedcom_importado_cuenta_la_historia_en_el_idioma_del_telefono()
    {
        var ged = "0 HEAD\n1 CHAR UTF-8\n0 @I1@ INDI\n1 NAME Ana /Lund/\n1 SEX F\n1 BIRT\n2 DATE BET 1919 AND 1920\n2 PLAC Lund\n1 FAMS @F1@\n" +
                  "0 @I2@ INDI\n1 NAME Per /Berg/\n1 SEX M\n1 FAMS @F1@\n1 DEAT\n2 DATE ABT 1990\n" +
                  "0 @F1@ FAM\n1 HUSB @I2@\n1 WIFE @I1@\n1 MARR\n2 DATE 1945\n0 TRLR\n";
        var datos = new UTF8Encoding(false).GetBytes(ged.Replace("\n", "\r\n"));
        string Historia(string idioma, string nombre)
        {
            using var cambio = Idioma.Probar(idioma);
            return Gedcom.Importar(datos, "x.ged", out _).Personas.Single(p => p.Nombre == nombre).Historia;
        }
        // el lugar de nacimiento va a la ficha; la fecha, como es de «entre», se queda en la historia
        Assert.Equal("Nació entre 1919 y 1920. Se casó con Per Berg en 1945.", Historia("es", "Ana"));
        Assert.Equal("Born between 1919 and 1920. Married Per Berg in 1945.", Historia("en", "Ana"));
        Assert.Equal("Née entre 1919 et 1920. Mariée à Per Berg en 1945.", Historia("fr", "Ana"));
        Assert.Equal("Nata tra il 1919 e il 1920. Sposata con Per Berg nel 1945.", Historia("it", "Ana"));
        Assert.Equal("Geboren zwischen 1919 und 1920. Heiratete Per Berg 1945.", Historia("de", "Ana"));
        Assert.Equal("Nasceu entre 1919 e 1920. Casou-se com Per Berg em 1945.", Historia("pt", "Ana"));
        Assert.Equal("Married Ana Lund in 1945.", Historia("en", "Per"));
        Assert.Equal("Marié à Ana Lund en 1945.", Historia("fr", "Per"));
    }

    [Fact]
    public void El_nombre_del_arbol_vuelve_del_gedcom_en_cualquier_idioma()
    {
        var a = Familias.Tipica();
        a.Nombre = "Familia «Pérez» de Ávila";
        byte[] datos;
        using (Idioma.Probar("de")) datos = Gedcom.Exportar(a);
        using (Idioma.Probar("fr")) Assert.Equal(a.Nombre, Gedcom.Importar(datos, "otro.ged", out _).Nombre);
        // y los que exportaron las primeras versiones, con la nota en español
        var viejo = "0 HEAD\n1 NOTE Árbol «Mi familia», exportado desde Ramo.\n0 @I1@ INDI\n1 NAME Ana //\n0 TRLR\n";
        Assert.Equal("Mi familia", Gedcom.Importar(new UTF8Encoding(false).GetBytes(viejo), "x.ged", out _).Nombre);
    }
}
