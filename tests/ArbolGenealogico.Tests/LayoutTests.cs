using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using Xunit.Abstractions;

namespace ArbolGenealogico.Tests;

public class LayoutTests
{
    private readonly ITestOutputHelper _out;
    public LayoutTests(ITestOutputHelper o) => _out = o;

    private static void ComprobarEstructura(Arbol a, LayoutResult r)
    {
        var alc = a.Alcanzables();
        Assert.Equal(alc.Count, r.Cartas.Count);                // todas las personas se colocan una vez
        foreach (var u in a.Uniones)
        {
            foreach (var h in u.Hijos)
                foreach (var p in u.Parejas)
                    Assert.Equal(r.Cartas[p].Y + r.Opciones.AltoCarta + r.Opciones.HuecoFilas, r.Cartas[h].Y, 3);  // padres una fila por encima
            if (u.Parejas.Count == 2)
                Assert.Equal(r.Cartas[u.Parejas[0]].Y, r.Cartas[u.Parejas[1]].Y, 3);
        }
    }

    [Fact]
    public void Tipica_esta_limpia()
    {
        var a = Familias.Tipica();
        var r = LayoutEngine.Calcular(a);
        ComprobarEstructura(a, r);
        var inf = Verificacion.Comprobar(r);
        _out.WriteLine(inf.ToString());
        Assert.True(inf.Limpio, inf.ToString());
    }

    [Fact]
    public void SegundasParejas_esta_limpia()
    {
        var a = Familias.SegundasParejas();
        var r = LayoutEngine.Calcular(a);
        ComprobarEstructura(a, r);
        var inf = Verificacion.Comprobar(r);
        Assert.True(inf.Limpio, inf.ToString());
    }

    [Fact]
    public void Pareja_adyacente_y_hombre_a_la_izquierda()
    {
        var a = Familias.Tipica();
        var r = LayoutEngine.Calcular(a);
        var padre = r.Cartas["p2"]; var madre = r.Cartas["p3"];
        Assert.True(padre.X < madre.X);
        Assert.Equal(r.Opciones.AnchoCarta + r.Opciones.HuecoPareja, madre.X - padre.X, 3);
    }

    [Fact]
    public void Solo_la_raiz()
    {
        var r = LayoutEngine.Calcular(Arbol.Nuevo("x", "a"));
        Assert.Single(r.Cartas);
        Assert.Empty(r.Conexiones);
    }

    [Theory]
    [InlineData(5, 12, 1, 0)]
    [InlineData(150, 25, 1, 0)]
    [InlineData(150, 45, 1, 0)]
    [InlineData(100, 80, 1, 3)]
    public void Familias_aleatorias_sin_solapes_ni_cruces(int cuantas, int pasos, int maxParejas, int toleradas)
    {
        int malas = 0; var detalles = new List<string>();
        for (int s = 1; s <= cuantas; s++)
        {
            int semilla = s * 7919 + pasos;
            var a = Familias.Aleatoria(semilla, pasos, maxParejas);
            var r = LayoutEngine.Calcular(a);
            ComprobarEstructura(a, r);
            var inf = Verificacion.Comprobar(r);
            if (!inf.Limpio) { malas++; if (detalles.Count < 5) detalles.Add($"semilla {semilla}:{pasos}:{maxParejas}: {inf}"); }
        }
        _out.WriteLine($"{malas}/{cuantas} con problemas");
        Assert.True(malas <= toleradas, $"{malas}/{cuantas} familias con problemas:\n" + string.Join("\n", detalles));
    }

    /// <summary>Tíos abuelos con descendencia: no hay dibujo plano posible; solo se mide y se exige que no haya solapes.</summary>
    [Fact]
    public void Hermanos_de_antepasados_sin_solapes()
    {
        int conCruces = 0, total = 150;
        for (int s = 1; s <= total; s++)
        {
            var a = Familias.Aleatoria(s * 15485863, 45, 1, hermanosDeAntepasados: true);
            var r = LayoutEngine.Calcular(a);
            ComprobarEstructura(a, r);
            var inf = Verificacion.Comprobar(r);
            Assert.Equal(0, inf.Solapes);
            Assert.Equal(0, inf.AristasSobreTarjetas);
            if (inf.CrucesAristas > 0) conCruces++;
        }
        _out.WriteLine($"tíos abuelos: {conCruces}/{total} con algún cruce");
    }

    /// <summary>Con segundas parejas nunca debe haber tarjetas solapadas; los cruces solo se miden.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Varias_parejas_sin_solapes(int maxParejas)
    {
        int conCruces = 0, total = 200;
        for (int s = 1; s <= total; s++)
        {
            var a = Familias.Aleatoria(s * 104729, 40, maxParejas);
            var r = LayoutEngine.Calcular(a);
            ComprobarEstructura(a, r);
            var inf = Verificacion.Comprobar(r);
            Assert.Equal(0, inf.Solapes);
            Assert.Equal(0, inf.AristasSobreTarjetas);
            if (inf.CrucesAristas > 0) conCruces++;
        }
        _out.WriteLine($"maxParejas={maxParejas}: {conCruces}/{total} con algún cruce");
    }
}
