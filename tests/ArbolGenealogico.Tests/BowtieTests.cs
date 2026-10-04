using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

/// <summary>Ordenación E («Bowtie»): antepasados en pajarita, los del padre a la izquierda y los de la madre a la derecha.</summary>
public class BowtieTests
{
    private static LayoutResult Calcular(Arbol a) => LayoutEngine.Calcular(a, new LayoutOptions { Ordenacion = Ordenacion.E });

    /// <summary>La persona principal y todos sus antepasados (y nadie más).</summary>
    private static HashSet<string> Esperados(Arbol a)
    {
        var res = new HashSet<string> { a.RaizId };
        var cola = new Queue<string>(res);
        while (cola.Count > 0)
            foreach (var p in a.UnionComoHijo(cola.Dequeue())?.Parejas ?? new List<string>())
                if (res.Add(p)) cola.Enqueue(p);
        return res;
    }

    [Fact]
    public void Padre_a_la_izquierda_madre_a_la_derecha_y_sus_antepasados_hacia_fuera()
    {
        var a = Arbol.Nuevo("t", "Yo");
        var padres = a.AnadirPadres(a.RaizId);
        a.Nombrar(padres[0].Id, "Padre", "", Sexo.Hombre); a.Nombrar(padres[1].Id, "Madre", "", Sexo.Mujer);
        var abP = a.AnadirPadres(padres[0].Id); var abM = a.AnadirPadres(padres[1].Id);
        a.AnadirPadres(abP[0].Id); a.AnadirPadres(abM[1].Id);
        a.AnadirHermano(a.RaizId); a.AnadirHijo(a.RaizId);           // no salen

        var r = Calcular(a);
        Assert.Equal(Esperados(a).OrderBy(x => x), r.Cartas.Keys.OrderBy(x => x));
        double x0 = r.Cartas[a.RaizId].X, y0 = r.Cartas[a.RaizId].Y;
        Assert.True(r.Cartas[padres[0].Id].X < x0 && r.Cartas[padres[1].Id].X > x0);
        Assert.Equal(y0, r.Cartas[padres[0].Id].Y, 3);
        Assert.Equal(y0, r.Cartas[padres[1].Id].Y, 3);
        foreach (var p in abP) Assert.True(r.Cartas[p.Id].X < r.Cartas[padres[0].Id].X);
        foreach (var p in abM) Assert.True(r.Cartas[p.Id].X > r.Cartas[padres[1].Id].X);
        // cada uno a media altura entre sus dos padres
        Assert.Equal((r.Cartas[abP[0].Id].Y + r.Cartas[abP[1].Id].Y) / 2, r.Cartas[padres[0].Id].Y, 3);
        var inf = Verificacion.Comprobar(r);
        Assert.True(inf.Limpio, inf.ToString());
    }

    [Theory]
    [InlineData(45, 1, true)]
    [InlineData(80, 1, false)]
    [InlineData(60, 3, false)]
    public void Familias_aleatorias_sin_cruces_ni_solapes(int pasos, int maxParejas, bool tios)
    {
        for (int s = 1; s <= 150; s++)
        {
            var a = Familias.Aleatoria(s * 7919 + pasos, pasos, maxParejas, hermanosDeAntepasados: tios);
            var r = Calcular(a);
            Assert.Equal(Esperados(a).Count, r.Cartas.Count);
            var inf = Verificacion.Comprobar(r);
            Assert.True(inf.Limpio, $"semilla {s}: {inf}");
        }
    }

    [Fact]
    public void Solo_la_raiz() => Assert.Single(Calcular(Arbol.Nuevo("x", "a")).Cartas);
}
