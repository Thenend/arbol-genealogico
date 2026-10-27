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

    [Fact]
    public void Bodas_de_parejas_vivas_y_juntas_si_se_piden()
    {
        var a = A(P("1", "Luis", "1950-01-01"), P("2", "Carmen", "1952-05-05"),       // 50 años de casados
                  P("3", "Pepe", null), P("4", "Rosa", null),                          // se separaron
                  P("5", "Juan", null, fallecido: true), P("6", "Ana", null),          // él ha muerto
                  P("7", "Jaime", null), P("8", "Eva", null));                          // sin día
        a.Uniones.Add(new Union { Id = "u1", Parejas = { "1", "2" }, Boda = Fecha.Leer("1976-10-26") });
        a.Uniones.Add(new Union { Id = "u2", Parejas = { "3", "4" }, Boda = Fecha.Leer("2000-10-26"), Separados = true });
        a.Uniones.Add(new Union { Id = "u3", Parejas = { "5", "6" }, Boda = Fecha.Leer("1970-10-26") });
        a.Uniones.Add(new Union { Id = "u4", Parejas = { "7", "8" }, Boda = Fecha.Leer("1990-10") });
        Assert.Empty(Cumpleanos.Del(Dia, new[] { ("a.ramo", a) }, false));
        var f = Assert.Single(Cumpleanos.Del(Dia, new[] { ("a.ramo", a) }, false, bodas: true));
        Assert.Equal("1", f.Id);
        Assert.Equal("Carmen y Luis cumplen 50 años de casados", f.Frase);
        using (Idioma.Probar("en"))
            Assert.Equal("Carmen and Luis celebrate 50 years of marriage", Cumpleanos.Del(Dia, new[] { ("a.ramo", a) }, false, true)[0].Frase);
    }

    [Fact]
    public void Primero_los_cumpleanos_luego_las_bodas_y_despues_las_muertes()
    {
        var a = A(P("1", "Luis", "1950-10-26"), P("2", "Carmen", null), P("3", "Abuelo", null, "2000-10-26"));
        a.Uniones.Add(new Union { Id = "u1", Parejas = { "1", "2" }, Boda = Fecha.Leer("2025-10-26") });
        var f = Cumpleanos.Del(Dia, new[] { ("a.ramo", a) }, true, true);
        Assert.Equal(new[] { "Luis cumple 76 años", "Carmen y Luis cumplen un año de casados", "Se cumplen 26 años de la muerte de Abuelo" },
                     f.Select(x => x.Frase));
    }
}
