using System.Text;
using ArbolGenealogico.Core.Idiomas;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;

namespace ArbolGenealogico.Tests;

public class FechaTests
{
    [Theory]
    [InlineData("1920")]
    [InlineData("1920-03")]
    [InlineData("1920-03-05")]
    [InlineData("~1850")]
    [InlineData("-0044-03-15")]
    [InlineData("~-0500")]
    [InlineData("2000-02-29")]
    [InlineData("-0001-02-29")]     // el 1 a. C. es bisiesto (el año 0 de los astrónomos)
    public void Se_guarda_y_se_lee_igual(string texto)
    {
        var f = Fecha.Leer(texto);
        Assert.NotNull(f);
        Assert.Equal(texto, f!.Value.Guardar());
    }

    [Theory]
    [InlineData("0000")]
    [InlineData("1920-13")]
    [InlineData("1920-04-31")]
    [InlineData("1900-02-29")]
    [InlineData("hacia 1900")]
    [InlineData("")]
    public void Lo_que_no_es_una_fecha_valida_no_se_lee(string texto) => Assert.Null(Fecha.Leer(texto));

    [Fact]
    public void Solo_el_ano_basta_y_el_dia_necesita_el_mes()
    {
        Assert.True(new Fecha(1920).EsValida);
        Assert.True(new Fecha(-44, 3, 15).EsValida);
        Assert.False(new Fecha(1920, null, 5).EsValida);
        Assert.False(new Fecha(0).EsValida);
        Assert.False(new Fecha(10000).EsValida);
    }

    [Fact]
    public void Se_comparan_con_lo_que_se_sabe_de_las_dos()
    {
        Assert.Equal(0, Fecha.Comparar(new Fecha(1920), new Fecha(1920, 3, 5)));
        Assert.True(Fecha.Comparar(new Fecha(1920, 2), new Fecha(1920, 3)) < 0);
        Assert.True(Fecha.Comparar(new Fecha(-44), new Fecha(14)) < 0);
    }

    [Fact]
    public void Van_en_el_archivo_y_los_arboles_sin_fechas_no_cambian()
    {
        var a = Familias.Tipica();
        var sin = ArbolJson.Serializar(a);
        Assert.DoesNotContain("nacimiento", sin);
        var p = a.Obtener(a.RaizId);
        p.Nacimiento = new Fecha(1920, 3, 5);
        p.Defuncion = new Fecha(-44, Aproximada: true);
        var texto = ArbolJson.Serializar(a);
        Assert.Contains("\"nacimiento\": \"1920-03-05\"", texto);
        Assert.Contains("\"defuncion\": \"~-0044\"", texto);
        var b = ArbolJson.Deserializar(texto, out _);
        Assert.Equal(p.Nacimiento, b.Obtener(a.RaizId).Nacimiento);
        Assert.Equal(p.Defuncion, b.Obtener(a.RaizId).Defuncion);
        // una fecha que no se entiende (escrita a mano, de otra versión…) se queda sin poner y el árbol se abre igual
        var raro = ArbolJson.Deserializar(texto.Replace("1920-03-05", "el martes").Replace("\"~-0044\"", "17"), out _);
        Assert.Null(raro.Obtener(a.RaizId).Nacimiento);
        Assert.Null(raro.Obtener(a.RaizId).Defuncion);
    }

    [Fact]
    public void Se_escriben_en_cada_idioma()
    {
        var p = new Persona { Nacimiento = new Fecha(1920, 3, 5), Defuncion = new Fecha(1998, Aproximada: true) };
        Assert.Equal("1920 – ~1998", Fechas.Anios(p));
        Assert.Equal("* 5 de marzo de 1920   † hacia 1998", Fechas.Vida(p));
        Assert.Equal("* 1950", Fechas.Anios(new Persona { Nacimiento = new Fecha(1950) }));
        Assert.Equal("† 1890", Fechas.Anios(new Persona { Defuncion = new Fecha(1890) }));
        Assert.Equal("", Fechas.Anios(new Persona()));
        Assert.Equal("15 de marzo de 44 a. C.", Fechas.Larga(new Fecha(-44, 3, 15)));
        using (Idioma.Probar("en"))
        {
            Assert.Equal("March 15, 44 BC", Fechas.Larga(new Fecha(-44, 3, 15)));
            Assert.Equal("* March 5, 1920   † about 1998", Fechas.Vida(p));
        }
        using (Idioma.Probar("de")) Assert.Equal("100 v. Chr. – 44 v. Chr.", Fechas.Anios(new Persona { Nacimiento = new Fecha(-100), Defuncion = new Fecha(-44) }));
    }

    [Theory]
    [InlineData("3 MAR 1920", "1920-03-03")]
    [InlineData("MAR 1920", "1920-03")]
    [InlineData("1920", "1920")]
    [InlineData("ABT 1850", "~1850")]
    [InlineData("EST 1850", "~1850")]
    [InlineData("44 B.C.", "-0044")]
    [InlineData("15 MAR 44 B.C.", "-0044-03-15")]
    [InlineData("INT 1920 (más o menos)", "1920")]
    [InlineData("@#DJULIAN@ 1 JAN 1700", "1700-01-01")]
    [InlineData("BEF 1900", null)]
    [InlineData("BET 1900 AND 1910", null)]
    [InlineData("1699/00", null)]
    [InlineData("second tuesday of spring", null)]
    [InlineData("31 FEB 1900", null)]
    public void Las_fechas_de_gedcom_que_son_una_sola_pasan_a_la_ficha(string gedcom, string? esperado) =>
        Assert.Equal(esperado, Gedcom.FechaDato(gedcom)?.Guardar());

    [Fact]
    public void El_gedcom_lleva_las_fechas_y_vuelven_igual()
    {
        var a = Familias.Tipica();
        var p = a.Obtener(a.RaizId);
        p.Nacimiento = new Fecha(1920, 3, 5);
        p.Defuncion = new Fecha(-44, Aproximada: true);
        var texto = Gedcom.ExportarTexto(a);
        Assert.Contains("1 BIRT\r\n2 DATE 5 MAR 1920\r\n", texto);
        Assert.Contains("1 DEAT\r\n2 DATE ABT 44 B.C.\r\n", texto);
        var b = Gedcom.Importar(Gedcom.Exportar(a), "x.ged", out _);
        var q = b.Obtener(a.RaizId);
        Assert.Equal(p.Nacimiento, q.Nacimiento);
        Assert.Equal(p.Defuncion, q.Defuncion);
        // y no se repiten en la historia
        Assert.Equal(p.Historia.Trim(), q.Historia);
    }

    [Fact]
    public void Una_fecha_de_entre_dos_se_queda_en_la_historia()
    {
        var ged = "0 HEAD\r\n0 @I1@ INDI\r\n1 NAME Ana /Lund/\r\n1 BIRT\r\n2 DATE BET 1900 AND 1905\r\n2 PLAC Lund\r\n0 TRLR\r\n";
        var p = Gedcom.Importar(new UTF8Encoding(false).GetBytes(ged), "x.ged", out _).Personas[0];
        Assert.Null(p.Nacimiento);
        Assert.Equal("Nació entre 1900 y 1905 en Lund.", p.Historia);
    }

    [Fact]
    public void Al_fusionar_se_completan_y_se_queda_la_mas_precisa()
    {
        var mio = Familias.Tipica();
        var otro = mio.Clonar();
        var ids = mio.Personas.Select(x => x.Id).ToList();
        otro.Obtener(ids[0]).Nacimiento = new Fecha(1920);            // yo no la tengo: se completa
        mio.Obtener(ids[1]).Nacimiento = new Fecha(1950);
        otro.Obtener(ids[1]).Nacimiento = new Fecha(1950, 6, 2);      // la suya sabe más: se queda la suya
        mio.Obtener(ids[2]).Defuncion = new Fecha(1990);
        otro.Obtener(ids[2]).Defuncion = new Fecha(1991);             // distintas: se elige
        var inf = Fusion.Fusionar(mio, otro);
        Assert.Equal(new Fecha(1920), mio.Obtener(ids[0]).Nacimiento);
        Assert.Equal(new Fecha(1950, 6, 2), mio.Obtener(ids[1]).Nacimiento);
        Assert.Equal(new Fecha(1990), mio.Obtener(ids[2]).Defuncion);
        Assert.Equal(2, inf.Completadas);
        Assert.Equal(new[] { ids[2] }, inf.ConDiferencias);
    }

    [Fact]
    public void Las_tarjetas_pueden_ir_sin_los_anos()
    {
        var f = FormaTarjeta.Leer("vertical foto sin-fechas");
        Assert.True(f.SinFechas);
        Assert.Equal("vertical foto sin-fechas", f.ToString());
        Assert.False(FormaTarjeta.Leer("horizontal foto").SinFechas);
        // sin tarjeta y sin apellidos, cada persona va en una línea… salvo que se vean sus años
        var a = Familias.Tipica();
        var forma = new FormaTarjeta(false, false, SinTarjeta: true, SinApellidos: true);
        Assert.True(forma.Para(a).SoloNombre);
        a.Personas[0].Nacimiento = new Fecha(1920);
        Assert.False(forma.Para(a).SoloNombre);
        Assert.True((forma with { SinFechas = true }).Para(a).SoloNombre);
    }
}
