using System.Globalization;
using ArbolGenealogico.Core.Idiomas;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Presentacion;
using Xunit;

namespace ArbolGenealogico.Tests;

/// <summary>Los papeles (también los de Estados Unidos) y la forma de repartir el árbol en hojas para imprimirlo.</summary>
public class ImpresionTests
{
    [Theory]
    [InlineData("US", "Letter")]
    [InlineData("CA", "Letter")]
    [InlineData("MX", "Letter")]
    [InlineData("ES", "A4")]
    [InlineData("BR", "A4")]
    [InlineData("GB", "A4")]
    [InlineData(null, "A4")]
    public void El_papel_habitual_depende_del_pais(string? pais, string papel) =>
        Assert.Equal(papel, Papel.Habitual(pais).Codigo);

    [Theory]
    [InlineData("en-US", "Letter")]
    [InlineData("es-MX", "Letter")]
    [InlineData("es-ES", "A4")]
    [InlineData("pt-BR", "A4")]
    [InlineData("en", "A4")]
    public void El_papel_habitual_de_una_cultura(string cultura, string papel) =>
        Assert.Equal(papel, Papel.HabitualDe(CultureInfo.GetCultureInfo(cultura)).Codigo);

    [Fact]
    public void Los_papeles_americanos_miden_lo_que_deben()
    {
        Assert.Equal((215.9, 279.4), (Papel.Carta.AnchoMm, Papel.Carta.AltoMm));
        Assert.Equal((215.9, 355.6), (Papel.Legal.AnchoMm, Papel.Legal.AltoMm));
        Assert.Equal((279.4, 431.8), (Papel.Tabloide.AnchoMm, Papel.Tabloide.AltoMm));
        Assert.Same(Papel.Tabloide, Papel.De("Tabloid"));
        Assert.Same(Papel.A4, Papel.De("desconocido"));
    }

    [Fact]
    public void Nombres_y_medidas_de_los_papeles()
    {
        Assert.Equal("21 × 29,7 cm", Papel.A4.Medidas);
        Assert.Equal("Carta", Papel.Carta.Nombre);
        Assert.Equal("8½ × 11 in  ·  21,6 × 27,9 cm", Papel.Carta.Medidas);
        Assert.Equal("11 × 17 in  ·  27,9 × 43,2 cm", Papel.Tabloide.Medidas);
        using (Idioma.Probar("en-US"))
        {
            Assert.Equal("Letter", Papel.Carta.Nombre);
            Assert.Equal("8½ × 14 in  ·  21.6 × 35.6 cm", Papel.Legal.Medidas);
        }
    }

    private static LayoutResult Arbol() => LayoutEngine.Calcular(Familias.Tipica());

    [Theory]
    [InlineData("A4", 1)]
    [InlineData("Letter", 1)]
    [InlineData("Legal", 3)]
    [InlineData("Tabloid", 4)]
    [InlineData("Letter", 6)]
    [InlineData("A4", 9)]
    [InlineData("A3", 10)]
    public void Cada_hoja_cabe_en_su_papel(string codigo, int hojas)
    {
        var papel = Papel.De(codigo);
        var c = PlanImpresion.Mejor(Arbol(), papel, hojas)!;
        Assert.Equal(hojas, c.Hojas);
        Assert.Same(papel, c.Papel);
        // cada trozo cabe en la zona imprimible de su hoja (la hoja sin los márgenes)
        for (int f = 0; f < c.Filas; f++)
            for (int col = 0; col < c.Columnas; col++)
            {
                var p = c.Pieza(f, col);
                Assert.True(p.Ancho * c.MmPorPx <= c.AnchoHojaMm - 2 * PlanImpresion.MargenMm + 1e-6);
                Assert.True(p.Alto * c.MmPorPx <= c.AltoHojaMm - 2 * PlanImpresion.MargenMm + 1e-6);
            }
        // y entre todas, el árbol entero
        var lay = Arbol();
        var o = lay.Opciones;
        Assert.True(c.Util.X <= lay.Cartas.Values.Min(t => t.X) - o.AnchoCarta / 2);
        Assert.True(c.Util.Derecha >= lay.Cartas.Values.Max(t => t.X) + o.AnchoCarta / 2);
    }

    [Fact]
    public void En_un_papel_mas_grande_el_arbol_sale_mas_grande()
    {
        var lay = Arbol();
        double Mm(Papel p) => PlanImpresion.Mejor(lay, p, 1)!.MmPorPx;
        Assert.True(Mm(Papel.Tabloide) > Mm(Papel.Carta));
        Assert.True(Mm(Papel.A3) > Mm(Papel.A4));
    }

    /// <summary>
    /// Con más hojas, el árbol sale más grande aunque haya que partir alguna tarjeta: no vale apilar las hojas en una columna
    /// para no cortar ninguna si así sale casi como en una sola (pasaba con 10 hojas y un árbol ancho).
    /// </summary>
    [Fact]
    public void Mas_hojas_no_se_quedan_en_el_tamano_de_una()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ArbolGenealogico.slnx"))) dir = Path.GetDirectoryName(dir);
        if (dir == null) return;
        var garcia = Core.Io.ArbolJson.Cargar(Path.Combine(dir, "Ejemplos", "familia-garcia.ramo"), out _);
        var lay = LayoutEngine.Calcular(garcia, new LayoutOptions { Ordenacion = Ordenacion.C });
        double una = PlanImpresion.Mejor(lay, Papel.A4, 1)!.MmPorPx;
        var diez = PlanImpresion.Mejor(lay, Papel.A4, 10)!;
        Assert.True(diez.MmPorPx > 2 * una, $"10 hojas: {diez.MmPorPx} mm/px; 1 hoja: {una}");
        Assert.True(diez.SePidioNoCortar);      // y avisa de las tarjetas que se cortan
    }

    [Fact]
    public void La_descripcion_en_cada_idioma()
    {
        var c = new PlanImpresion.Configuracion(Papel.Carta, false, 1, 4, 0.1, new double[] { 0, 1, 2, 3, 4 }, new double[] { 0, 1 }, 0);
        Assert.Equal("4 hojas Carta horizontales, una al lado de otra  ·  nombres en letra de 7,5 pt (se lee bien)", c.Descripcion(26.46));
        using (Idioma.Probar("en"))
            Assert.Equal("4 Letter sheets, landscape, side by side  ·  names in 7.5 pt type (readable)", c.Descripcion(26.46));
        using (Idioma.Probar("de"))
            Assert.Equal("4 Blätter Letter quer, nebeneinander", c.Reparto);
        var una = c with { Columnas = 1, CortesX = new double[] { 0, 1 }, Vertical = true, Papel = Papel.A4, TarjetasCortadas = 2 };
        Assert.Equal("1 hoja A4 vertical  ·  nombres en letra de 7,5 pt (se lee bien)  ·  los cortes atraviesan 2 tarjetas", una.Descripcion(26.46));
        Assert.Equal(PlanImpresion.Lectura.MuyPequena, PlanImpresion.Configuracion.Legibilidad(3.9));
        Assert.Equal(PlanImpresion.Lectura.Comoda, PlanImpresion.Configuracion.Legibilidad(8));
    }
}
