using System.Text;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

public class GedcomTests
{
    private static byte[] Utf8(string s) => new UTF8Encoding(false).GetBytes(s.Replace("\n", "\r\n"));

    [Fact]
    public void Exportar_e_importar_devuelve_el_mismo_arbol()
    {
        var a = Familias.Tipica();
        a.Obtener(a.RaizId).Historia = "Primera línea.\nSegunda línea con una @ arroba.\n\n" + string.Join(" ", Enumerable.Repeat("palabra", 120));
        var datos = Gedcom.Exportar(a, "tipica.ged");
        Assert.True(Gedcom.PareceGedcom(datos));
        var b = Gedcom.Importar(datos, "tipica.ged", out _);

        Assert.Equal(a.Personas.Count, b.Personas.Count);
        Assert.Equal(a.RaizId, b.RaizId);
        Assert.Equal(a.Nombre, b.Nombre);
        foreach (var p in a.Personas)
        {
            var q = b.Obtener(p.Id);     // los ids se conservan (_UID)
            Assert.Equal(p.Nombre.Trim(), q.Nombre);
            Assert.Equal(p.Apellidos.Trim(), q.Apellidos);
            Assert.Equal(p.Sexo, q.Sexo);
            Assert.Equal(p.Historia.Trim(), q.Historia);
        }
        // las mismas familias: mismas parejas y mismos hijos en el mismo orden
        string Clave(Union u) => string.Join(",", u.Parejas.OrderBy(x => x)) + "|" + string.Join(",", u.Hijos);
        Assert.Equal(a.Uniones.Select(Clave).OrderBy(x => x), b.Uniones.Select(Clave).OrderBy(x => x));
        // y una fusión con el original no añade a nadie
        Assert.False(Fusion.Probar(a, b).AportaAlgo);
    }

    [Fact]
    public void Las_lineas_del_archivo_no_pasan_de_255_caracteres()
    {
        var a = Familias.Tipica();
        a.Obtener(a.RaizId).Historia = new string('x', 5000) + " " + string.Join(" ", Enumerable.Repeat("a b", 900));
        var texto = Gedcom.ExportarTexto(a);
        Assert.All(texto.Split("\r\n"), l => Assert.True(l.Length <= 255, l));
        Assert.StartsWith("0 HEAD", texto);
        Assert.EndsWith("0 TRLR\r\n", texto);
    }

    [Fact]
    public void Importa_un_gedcom_de_otro_programa_con_fechas_lugares_y_notas()
    {
        var ged = """
            0 HEAD
            1 SOUR PAF
            1 GEDC
            2 VERS 5.5.1
            1 CHAR UTF-8
            0 @I1@ INDI
            1 NAME Juan /PÉREZ/ García
            1 SEX M
            1 BIRT
            2 DATE 3 MAR 1920
            2 PLAC Ávila, España
            1 DEAT
            2 DATE ABT 1998
            1 OCCU Agricultor
            1 NOTE Le gustaba
            2 CONC  cantar.
            2 CONT Y bailar.
            1 FAMS @F1@
            0 @I2@ INDI
            1 NAME María /López/
            1 SEX F
            1 CHR
            2 DATE BET 1921 AND 1922
            1 NOTE @N1@
            1 FAMS @F1@
            0 @I3@ INDI
            1 NAME Ana /Pérez López/
            1 SEX F
            1 FAMC @F1@
            0 @I4@ INDI
            1 NAME Suelto /Sin Familia/
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I3@
            1 MARR
            2 DATE 12 MAY 1945
            2 PLAC Madrid
            0 @N1@ NOTE Nota compartida
            1 CONT de varias líneas.
            0 TRLR
            """;
        var a = Gedcom.Importar(Utf8(ged), "Familia Pérez.ged", out var avisos);

        Assert.Equal("Familia Pérez", a.Nombre);
        Assert.Equal(4, a.Personas.Count);
        var juan = a.Personas.Single(p => p.Nombre == "Juan");
        Assert.Equal("Pérez García", juan.Apellidos);           // mayúsculas normalizadas y lo de detrás de la barra
        Assert.Equal(Sexo.Hombre, juan.Sexo);
        // en el orden de una vida: la muerte, la última
        Assert.Equal("Oficio: Agricultor. " +
                     "Se casó con María López el 12 de mayo de 1945 en Madrid.\n\nLe gustaba cantar.\nY bailar.", juan.Historia);
        // el lugar de nacimiento también va a la ficha
        Assert.Equal("Ávila, España", juan.LugarNacimiento);
        // las fechas de nacimiento y defunción van a la ficha como dato (la aproximada, marcada)
        Assert.Equal(new Fecha(1920, 3, 3), juan.Nacimiento);
        Assert.Equal(new Fecha(1998, Aproximada: true), juan.Defuncion);
        var maria = a.Personas.Single(p => p.Nombre == "María");
        Assert.Equal("Fue bautizada entre 1921 y 1922. Se casó con Juan Pérez García el 12 de mayo de 1945 en Madrid.\n\n" +
                     "Nota compartida\nde varias líneas.", maria.Historia);
        var u = a.Uniones.Single();
        Assert.Equal(new[] { juan.Id, maria.Id }, u.Parejas);
        Assert.Equal(a.Personas.Single(p => p.Nombre == "Ana").Id, u.Hijos.Single());
        Assert.Equal(juan.Id, a.RaizId);                         // del grupo más grande
        Assert.Contains(avisos, x => x.Contains("no está conectada"));
        // el mismo archivo da los mismos ids (importarlo dos veces y fusionar no duplica)
        var otra = Gedcom.Importar(Utf8(ged), "x.ged", out _);
        Assert.Equal(a.Personas.Select(p => p.Id), otra.Personas.Select(p => p.Id));
    }

    [Fact]
    public void Lee_ansel_y_windows_1252()
    {
        // ANSEL: el acento va delante de la letra (E2 = agudo, E4 = tilde)
        var ansel = new List<byte>();
        ansel.AddRange(Encoding.ASCII.GetBytes("0 HEAD\r\n1 CHAR ANSEL\r\n0 @I1@ INDI\r\n1 NAME Jos"));
        ansel.AddRange(new byte[] { 0xE2, (byte)'e' });
        ansel.AddRange(Encoding.ASCII.GetBytes(" /Mu"));
        ansel.AddRange(new byte[] { 0xE4, (byte)'n' });
        ansel.AddRange(Encoding.ASCII.GetBytes("oz/\r\n0 TRLR\r\n"));
        var a = Gedcom.Importar(ansel.ToArray(), "a.ged", out _);
        Assert.Equal("José", a.Personas[0].Nombre);
        Assert.Equal("Muñoz", a.Personas[0].Apellidos);

        var w = new List<byte>();
        w.AddRange(Encoding.ASCII.GetBytes("0 HEAD\r\n1 CHAR ANSI\r\n0 @I1@ INDI\r\n1 NAME Ram"));
        w.Add(0xF3);       // ó
        w.AddRange(Encoding.ASCII.GetBytes("n /Pe"));
        w.Add(0xF1);       // ñ
        w.AddRange(Encoding.ASCII.GetBytes("a/\r\n0 TRLR\r\n"));
        var b = Gedcom.Importar(w.ToArray(), "b.ged", out _);
        Assert.Equal("Ramón", b.Personas[0].Nombre);
        Assert.Equal("Peña", b.Personas[0].Apellidos);
    }

    [Fact]
    public void Hermanos_de_padres_desconocidos_siguen_juntos()
    {
        var ged = """
            0 HEAD
            0 @I1@ INDI
            1 NAME Uno //
            0 @I2@ INDI
            1 NAME Dos //
            0 @F1@ FAM
            1 CHIL @I1@
            1 CHIL @I2@
            0 TRLR
            """;
        var a = Gedcom.Importar(Utf8(ged), "h.ged", out _);
        Assert.Equal(3, a.Personas.Count);
        Assert.Equal(2, a.Uniones.Single().Hijos.Count);
        Assert.Equal(a.Personas.Count, a.Alcanzables().Count);
    }

    [Fact]
    public void La_misma_pareja_casada_dos_veces_es_una_sola_union()
    {
        var ged = """
            0 HEAD
            1 CHAR UTF-8
            0 @I1@ INDI
            1 NAME Peter /Sweet/
            1 SEX M
            0 @I2@ INDI
            1 NAME Mary /Encore/
            1 SEX F
            0 @I3@ INDI
            1 NAME Juan /Donalds/
            1 SEX M
            0 @I4@ INDI
            1 NAME Hija /Sweet/
            0 @F1@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 MARR
            2 DATE 1 Jan 1900
            1 DIV
            2 DATE 10 Oct 1910
            0 @F2@ FAM
            1 HUSB @I3@
            1 WIFE @I2@
            0 @F3@ FAM
            1 HUSB @I1@
            1 WIFE @I2@
            1 CHIL @I4@
            1 MARR
            2 DATE 1 Jan 1912
            0 TRLR
            """;
        var a = Gedcom.Importar(Utf8(ged), "r.ged", out _);
        Assert.Equal(2, a.Uniones.Count);
        var peter = a.Personas.Single(p => p.Nombre == "Peter");
        Assert.Single(a.UnionesComoPareja(peter.Id).Single().Hijos);
        Assert.Equal("Se casó con Mary Encore el 1 de enero de 1900. Se divorció de Mary Encore el 10 de octubre de 1910. " +
                     "Se casó con Mary Encore el 1 de enero de 1912.", peter.Historia);
        Assert.True(Core.Layout.Verificacion.Comprobar(Core.Layout.LayoutEngine.Calcular(a)).Limpio);
    }

    [Fact]
    public void Adopcion_emigracion_y_fechas_que_no_son_fechas()
    {
        var ged = """
            0 HEAD
            0 @I1@ INDI
            1 NAME Gustav /Lindqvist/
            1 SEX M
            1 BIRT
            2 DATE second tuesday of spring
            1 ADOP
            2 DATE 16 Mar 1924
            1 EMIG
            2 DATE ABT 1948
            2 PLAC Göteborg
            1 IMMI
            2 DATE 1948
            2 PLAC Minneapolis
            0 TRLR
            """;
        var a = Gedcom.Importar(Utf8(ged), "g.ged", out _);
        Assert.Equal("Nació (second tuesday of spring). Fue adoptado el 16 de marzo de 1924. " +
                     "Emigró hacia 1948 (Göteborg). Llegó a Minneapolis en 1948.", a.Personas[0].Historia);
    }

    [Fact]
    public void Un_gedcom_sin_personas_no_es_un_arbol()
    {
        var ged = "0 HEAD\n1 CHAR UTF-8\n0 @U@ SUBM\n1 NAME gedcom.org\n0 TRLR\n";
        var e = Assert.Throws<InvalidDataException>(() => Gedcom.Importar(Utf8(ged), "m.ged", out _));
        Assert.Contains("ninguna persona", e.Message);
    }

    [Theory]
    [InlineData("3 MAR 1920", "el 3 de marzo de 1920")]
    [InlineData("2 Oct 1822", "el 2 de octubre de 1822")]
    [InlineData("from 1900 to 1905", "desde 1900 hasta 1905")]
    [InlineData("MAR 1920", "en marzo de 1920")]
    [InlineData("1920", "en 1920")]
    [InlineData("ABT 1900", "hacia 1900")]
    [InlineData("BEF 12 JAN 1900", "antes del 12 de enero de 1900")]
    [InlineData("AFT 1900", "después de 1900")]
    [InlineData("FROM 1900 TO 1910", "desde 1900 hasta 1910")]
    [InlineData("(en la guerra)", "(en la guerra)")]
    [InlineData("@#DJULIAN@ 1 JAN 1700", "el 1 de enero de 1700")]
    public void Fechas_en_espanol(string gedcom, string esperado) => Assert.Equal(esperado, Gedcom.FraseFecha(gedcom));

    [Fact]
    public void No_confunde_un_tree_con_un_gedcom()
    {
        var tree = new UTF8Encoding(false).GetBytes(ArbolJson.Serializar(Familias.Tipica()));
        Assert.False(Gedcom.PareceGedcom(tree));
        Assert.Throws<InvalidDataException>(() => Gedcom.Importar(tree, "x.ged", out _));
    }
}
