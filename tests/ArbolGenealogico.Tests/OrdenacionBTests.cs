using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using Xunit.Abstractions;

namespace ArbolGenealogico.Tests;

/// <summary>Ordenación B (estilo Family TreePhoto).</summary>
public class OrdenacionBTests
{
    private readonly ITestOutputHelper _out;
    public OrdenacionBTests(ITestOutputHelper o) => _out = o;

    private static LayoutResult Calcular(Arbol a) => LayoutEngine.Calcular(a, new LayoutOptions { Ordenacion = Ordenacion.B });

    private static void ComprobarEstructura(Arbol a, LayoutResult r)
    {
        var alc = a.Alcanzables();
        Assert.Equal(alc.Count, r.Cartas.Count);                       // nadie se queda fuera ni sale dos veces
        foreach (var u in a.Uniones)
        {
            foreach (var h in u.Hijos)
                foreach (var p in u.Parejas)
                    Assert.True(r.Cartas[p].Y < r.Cartas[h].Y, $"{p} debe quedar por encima de su hijo {h}");
            if (u.Parejas.Count == 2)
                Assert.Equal(r.Cartas[u.Parejas[0]].Y, r.Cartas[u.Parejas[1]].Y, 3);
        }
    }

    /// <summary>El árbol de Mario: abuelos de las dos ramas con hermanos y descendencia.</summary>
    private static (Arbol a, List<Persona> padres, List<Persona> ab1, List<Persona> ab2) Mario()
    {
        var a = Arbol.Nuevo("t", "Mario");
        a.Obtener(a.RaizId).Sexo = Sexo.Hombre;
        var padres = a.AnadirPadres(a.RaizId);
        a.Nombrar(padres[0].Id, "Alberto", "", Sexo.Hombre); a.Nombrar(padres[1].Id, "Adela", "", Sexo.Mujer);
        var ab1 = a.AnadirPadres(padres[0].Id); a.Nombrar(ab1[0].Id, "Sinesio", "", Sexo.Hombre); a.Nombrar(ab1[1].Id, "Irene", "", Sexo.Mujer);
        var ab2 = a.AnadirPadres(padres[1].Id); a.Nombrar(ab2[0].Id, "Ricardo", "", Sexo.Hombre); a.Nombrar(ab2[1].Id, "Cleo", "", Sexo.Mujer);
        a.AnadirPadres(ab1[0].Id); a.AnadirPadres(ab1[1].Id); a.AnadirPadres(ab2[0].Id); a.AnadirPadres(ab2[1].Id);
        var eustaquio = a.AnadirHermano(ab2[0].Id).hermano; a.Nombrar(eustaquio.Id, "Eustaquio", "", Sexo.Hombre);
        var marcelina = a.AnadirHijo(eustaquio.Id); a.Nombrar(marcelina.Id, "Marcelina", "", Sexo.Mujer);
        a.AnadirHijo(marcelina.Id);
        var tioIrene = a.AnadirHermano(ab1[1].Id).hermano; a.AnadirHijo(tioIrene.Id); a.AnadirHijo(tioIrene.Id);
        var tioSinesio = a.AnadirHermano(ab1[0].Id).hermano; a.AnadirHijo(tioSinesio.Id);
        var tiaCleo = a.AnadirHermano(ab2[1].Id).hermano; a.AnadirHijo(tiaCleo.Id);
        var hAlberto = a.AnadirHermano(padres[0].Id).hermano; a.AnadirHijo(hAlberto.Id);
        var hAdela = a.AnadirHermano(padres[1].Id).hermano; a.AnadirHijo(hAdela.Id);
        return (a, padres, ab1, ab2);
    }

    [Fact]
    public void Arbol_de_Mario_sin_cruces_ni_solapes()
    {
        var (a, padres, ab1, ab2) = Mario();
        var r = Calcular(a);
        ComprobarEstructura(a, r);
        var inf = Verificacion.Comprobar(r);
        Assert.True(inf.Limpio, inf.ToString());

        // la mujer a la izquierda en cada pareja de antepasados
        Assert.True(r.Cartas[padres[1].Id].X < r.Cartas[padres[0].Id].X, "Adela a la izquierda de Alberto");
        Assert.True(r.Cartas[ab1[1].Id].X < r.Cartas[ab1[0].Id].X, "Irene a la izquierda de Sinesio");
        // cada antepasado, en la vertical del punto de unión de sus padres, y por debajo de sus hermanos
        var enlace = r.Conexiones.First(c => c.Tipo == TipoConexion.Pareja && c.UnionId == a.UnionComoHijo(padres[1].Id)!.Id);
        Assert.Equal(enlace.Nudo!.Value.X, r.Cartas[padres[1].Id].X, 3);
        var hermanoAdela = a.UnionComoHijo(padres[1].Id)!.Hijos.First(h => h != padres[1].Id);
        Assert.True(r.Cartas[padres[1].Id].Y > r.Cartas[hermanoAdela].Y);
        // los cónyuges de la pareja de antepasados no van pegados: entre ellos queda el hueco de la línea de los hijos
        Assert.True(r.Cartas[padres[0].Id].X - r.Cartas[padres[1].Id].X > 2 * r.Opciones.AnchoCarta);
    }

    [Fact]
    public void Tipica_y_segundas_parejas_completas_y_sin_solapes()
    {
        foreach (var a in new[] { Familias.Tipica(), Familias.SegundasParejas() })
        {
            var r = Calcular(a);
            ComprobarEstructura(a, r);
            var inf = Verificacion.Comprobar(r);
            _out.WriteLine(inf.ToString());
            Assert.Equal(0, inf.Solapes);
            Assert.True(inf.Limpio, inf.ToString());
        }
    }

    [Fact]
    public void Solo_la_raiz() => Assert.Single(Calcular(Arbol.Nuevo("x", "a")).Cartas);

    [Theory]
    [InlineData(45, 1, true, 0)]
    [InlineData(80, 1, false, 0)]
    [InlineData(45, 2, true, -1)]
    [InlineData(60, 3, false, -1)]
    public void Familias_aleatorias(int pasos, int maxParejas, bool tios, int crucesToleradas)
    {
        int conCruces = 0, total = 150;
        var detalles = new List<string>();
        for (int s = 1; s <= total; s++)
        {
            var a = Familias.Aleatoria(s * 7919 + pasos, pasos, maxParejas, hermanosDeAntepasados: tios);
            var r = Calcular(a);
            ComprobarEstructura(a, r);
            var inf = Verificacion.Comprobar(r);
            Assert.True(inf.Solapes == 0, $"semilla {s}: {inf}");
            if (!inf.Limpio) { conCruces++; if (detalles.Count < 3) detalles.Add($"semilla {s}: {inf}"); }
        }
        _out.WriteLine($"pasos={pasos} parejas={maxParejas} tíos={tios}: {conCruces}/{total} con algún cruce");
        if (crucesToleradas >= 0) Assert.True(conCruces <= crucesToleradas, string.Join("\n", detalles));
    }
}
