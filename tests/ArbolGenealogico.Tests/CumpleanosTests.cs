using ArbolGenealogico.Core.Idiomas;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;

namespace ArbolGenealogico.Tests;

public class CumpleanosTests
{
    private static Persona P(string id, string nombre, string? nacimiento, string? defuncion = null, bool fallecido = false) => new()
    {
        Id = id, Nombre = nombre, Nacimiento = Fecha.Leer(nacimiento), Defuncion = Fecha.Leer(defuncion), Fallecido = fallecido,
    };

    private static Arbol A(params Persona[] personas)
    {
        var a = new Arbol();
        a.Personas.AddRange(personas);
        return a;
    }

    private static readonly DateTime Dia = new(2026, 10, 26);

    [Fact]
    public void Solo_vivos_con_dia_y_mes_y_edad_creible()
    {
        var a = A(
            P("1", "Carmen", "1946-10-26"),
            P("2", "Luis", "1946-10"),                         // sin día
            P("3", "Rosa", "~1946-10-26"),                     // aproximada
            P("4", "Pepe", "1900-10-26"),                      // 126 años: ya no vive
            P("5", "Ana", "1950-10-26", fallecido: true),
            P("6", "Juan", "1980-10-27"),
            P("7", "", "1980-10-26"),                          // sin nombre
            P("8", "Hoy", "2026-10-26"));                      // nace hoy: no es su cumpleaños
        var f = Cumpleanos.Del(Dia, new[] { ("a.ramo", a) }, aniversarios: false);
        Assert.Equal(new[] { "Carmen" }, f.Select(x => x.Nombre));
        Assert.Equal(80, f[0].Anios);
        Assert.Equal("Carmen cumple 80 años", f[0].Frase);
    }

    [Fact]
    public void Aniversarios_solo_si_se_piden()
    {
        var a = A(P("1", "Luis", "1920-03-01", "2016-10-26"), P("2", "Carmen", "1946-10-26"));
        Assert.Single(Cumpleanos.Del(Dia, new[] { ("a.ramo", a) }, false));
        var f = Cumpleanos.Del(Dia, new[] { ("a.ramo", a) }, true);
        Assert.Equal(new[] { "Carmen", "Luis" }, f.Select(x => x.Nombre));
        Assert.Equal("Se cumplen 10 años de la muerte de Luis", f[1].Frase);
        using (Idioma.Probar("en")) Assert.Equal("10 years since Luis passed away", f[1].Frase);
    }

    [Fact]
    public void Quien_esta_en_dos_arboles_sale_una_vez_y_con_el_primero()
    {
        var uno = A(P("1", "Carmen", "1946-10-26"));
        var otro = A(P("1", "Carmen", "1946-10-26"), P("9", "Carmen", "1946-10-26"));
        var f = Cumpleanos.Del(Dia, new[] { ("uno.ramo", uno), ("otro.ramo", otro) }, false);
        Assert.Equal("uno.ramo", Assert.Single(f).Ruta);
    }

    [Fact]
    public void El_29_de_febrero_se_celebra_el_28_si_no_es_bisiesto()
    {
        var a = A(P("1", "Bisi", "2000-02-29"));
        Assert.Single(Cumpleanos.Del(new DateTime(2027, 2, 28), new[] { ("a", a) }, false));
        Assert.Empty(Cumpleanos.Del(new DateTime(2028, 2, 28), new[] { ("a", a) }, false));
        Assert.Single(Cumpleanos.Del(new DateTime(2028, 2, 29), new[] { ("a", a) }, false));
    }
}
