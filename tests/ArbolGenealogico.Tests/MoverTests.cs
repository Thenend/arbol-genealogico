using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

/// <summary>Cambiar el parentesco de una persona mal colocada.</summary>
public class MoverTests
{
    private static Vinculo ComoHijo(Arbol a, string id) => new(a.UnionComoHijo(id)!.Id, true);

    private static void Coherente(Arbol a)
    {
        Assert.Empty(a.Clonar().Reparar());
        Assert.Equal(a.Personas.Count, a.Alcanzables().Count);
        var r = LayoutEngine.Calcular(a);
        Assert.Equal(a.Personas.Count, r.Cartas.Count);
        foreach (var u in a.Uniones)
            foreach (var h in u.Hijos)
                foreach (var p in u.Parejas)
                    Assert.True(r.Cartas[p].Y < r.Cartas[h].Y);
    }

    [Fact]
    public void Hermano_puesto_como_tio_pasa_a_ser_hermano_con_su_familia()
    {
        var a = Familias.Tipica();
        var juan = a.IdDe("Tío Juan");
        int personas = a.Personas.Count;
        var r = a.Mover(juan, ComoHijo(a, juan), Parentesco.Hermano, a.RaizId);
        Assert.Same(a.UnionComoHijo(a.RaizId), a.UnionComoHijo(juan));
        Assert.Equal(a.UnionComoHijo(a.RaizId)!.Hijos.IndexOf(a.RaizId) + 1, a.UnionComoHijo(a.RaizId)!.Hijos.IndexOf(juan));
        // su mujer, sus 3 hijos y el nieto van con él
        Assert.Equal(5, r.Acompanan);
        Assert.Empty(r.Quitadas);
        Assert.Equal(personas, a.Personas.Count);
        Assert.Contains(a.IdDe("Primo 1"), a.UnionesComoPareja(juan).SelectMany(u => u.Hijos));
        Assert.DoesNotContain(juan, a.UnionComoHijo(a.IdDe("Padre"))!.Hijos);
        Coherente(a);
    }

    [Fact]
    public void Los_padres_vacios_puestos_para_colgar_al_hermano_se_quitan()
    {
        var a = Arbol.Nuevo("Prueba", "Yo");
        var padres = a.AnadirPadres(a.RaizId);
        a.Nombrar(padres[0].Id, "Padre", "", Sexo.Hombre);
        a.Nombrar(padres[1].Id, "Madre", "", Sexo.Mujer);
        // «hermano» del padre, que no tenía padres: se le ponen dos vacíos
        var h = a.AnadirHermano(padres[0].Id).hermano; a.Nombrar(h.Id, "Luis");
        Assert.Equal(6, a.Personas.Count);
        a.Mover(h.Id, ComoHijo(a, h.Id), Parentesco.Hermano, a.RaizId);
        Assert.Equal(4, a.Personas.Count);
        Assert.Null(a.UnionComoHijo(padres[0].Id));
        Assert.Same(a.UnionComoHijo(a.RaizId), a.UnionComoHijo(h.Id));
        Coherente(a);
    }

    [Fact]
    public void Los_padres_con_datos_no_se_quitan()
    {
        var a = Familias.Tipica();
        var marta = a.IdDe("Tía Marta");
        // pasa a ser prima: hija del tío Juan y la tía Rosa
        a.Mover(marta, ComoHijo(a, marta), Parentesco.Hijo, a.IdDe("Tío Juan"));
        Assert.NotNull(a.Buscar(a.IdDe("Abuelo paterno")));
        Assert.Contains(marta, a.UnionComoHijo(a.IdDe("Primo 1"))!.Hijos);
        Coherente(a);
    }

    [Fact]
    public void Madre_puesta_como_hermana_ocupa_el_hueco_vacio()
    {
        var a = Arbol.Nuevo("Prueba", "Yo");
        var padres = a.AnadirPadres(a.RaizId);
        a.Nombrar(padres[0].Id, "Padre", "", Sexo.Hombre);   // la madre se queda vacía
        var ana = a.AnadirHermano(a.RaizId).hermano; a.Nombrar(ana.Id, "Ana", "", Sexo.Mujer);
        a.Mover(ana.Id, ComoHijo(a, ana.Id), Parentesco.PadreOMadre, a.RaizId);
        Assert.Equal(new[] { padres[0].Id, ana.Id }, a.UnionComoHijo(a.RaizId)!.Parejas);
        Assert.Null(a.Buscar(padres[1].Id));
        Assert.Equal(3, a.Personas.Count);
        Coherente(a);
    }

    [Fact]
    public void Con_dos_padres_con_datos_no_cabe_otro()
    {
        var a = Familias.Tipica();
        var juan = a.IdDe("Tío Juan");
        var (r, error) = a.ProbarMover(juan, ComoHijo(a, juan), Parentesco.PadreOMadre, a.RaizId);
        Assert.Null(r);
        Assert.Equal("Ya tiene los dos padres.", error);
    }

    [Fact]
    public void Pareja_equivocada_se_lleva_o_deja_a_los_hijos()
    {
        foreach (var conElla in new[] { false, true })
        {
            var a = Familias.Tipica();
            var rosa = a.IdDe("Tía Rosa");
            var juan = a.IdDe("Tío Juan");
            var primo = a.IdDe("Primo materno 1");
            var u = a.UnionesComoPareja(rosa).Single();
            a.Mover(rosa, new Vinculo(u.Id, false), Parentesco.Pareja, primo, hijosConElla: conElla);
            Assert.Contains(a.UnionesComoPareja(rosa), x => x.Parejas.Contains(primo));
            Assert.DoesNotContain(a.UnionesComoPareja(juan), x => x.Parejas.Contains(rosa));
            var dePrimo1 = a.UnionComoHijo(a.IdDe("Primo 1"))!;
            Assert.Equal(conElla ? new[] { rosa, primo } : new[] { juan }, dePrimo1.Parejas);
            Coherente(a);
        }
    }

    [Fact]
    public void Nadie_puede_ser_hijo_de_su_hijo_ni_moverse_a_su_rama()
    {
        var a = Familias.Tipica();
        var juan = a.IdDe("Tío Juan");
        var copia = ArbolGenealogico.Core.Io.ArbolJson.Serializar(a);
        Assert.NotNull(a.ProbarMover(juan, ComoHijo(a, juan), Parentesco.Hijo, a.IdDe("Primo 1")).Error);
        Assert.NotNull(a.ProbarMover(juan, ComoHijo(a, juan), Parentesco.Hermano, a.IdDe("Primo 2")).Error);
        Assert.NotNull(a.ProbarMover(juan, ComoHijo(a, juan), Parentesco.Pareja, a.IdDe("Tía Rosa")).Error);
        // el padre no puede pasar a ser hijo de su propio hijo
        var padre = a.IdDe("Padre");
        var (_, error) = a.ProbarMover(padre, new Vinculo(a.UnionesComoPareja(padre).Single().Id, false), Parentesco.Hijo, a.IdDe("Hermana"));
        Assert.NotNull(error);
        Assert.Throws<InvalidOperationException>(() => a.Mover(juan, ComoHijo(a, juan), Parentesco.Hijo, a.IdDe("Primo 1")));
        Assert.Equal(copia, ArbolGenealogico.Core.Io.ArbolJson.Serializar(a));   // lo que no se puede no cambia nada
    }

    [Fact]
    public void Lo_que_no_tiene_sentido_no_se_hace()
    {
        var a = Familias.Tipica();
        Assert.NotNull(a.PorQueNoSeMueve(a.RaizId));
        var juan = a.IdDe("Tío Juan");
        // ya es así
        Assert.Equal("Ya es así en el árbol.", a.ProbarMover(juan, ComoHijo(a, juan), Parentesco.Hermano, a.IdDe("Padre")).Error);
        Assert.Equal("Elige a otra persona.", a.ProbarMover(juan, ComoHijo(a, juan), Parentesco.Hermano, juan).Error);
        // hermanos de una pareja política: no
        Assert.NotNull(a.ProbarMover(juan, ComoHijo(a, juan), Parentesco.Hermano, a.IdDe("Mi esposa")).Error);
        // el tío Juan (con padres) quitado de su mujer y puesto como pareja de otra: sigue siendo de la familia, vale
        var u = a.UnionesComoPareja(juan).Single();
        Assert.Null(a.ProbarMover(juan, new Vinculo(u.Id, false), Parentesco.Pareja, a.IdDe("Tía Elena")).Error);
        // con su sobrina, no: serían de dos generaciones
        Assert.NotNull(a.ProbarMover(juan, new Vinculo(u.Id, false), Parentesco.Pareja, a.IdDe("Prima Lucía")).Error);
    }

    [Fact]
    public void Movimientos_al_azar_dejan_el_arbol_coherente()
    {
        var rnd = new Random(7);
        int hechos = 0;
        for (int semilla = 0; semilla < 40; semilla++)
        {
            var a = Familias.Aleatoria(semilla, 40);
            for (int k = 0; k < 15; k++)
            {
                var p = a.Personas[rnd.Next(a.Personas.Count)];
                if (a.PorQueNoSeMueve(p.Id) != null) continue;
                var vs = a.Vinculos(p.Id);
                var v = vs[rnd.Next(vs.Count)];
                var otro = a.Personas[rnd.Next(a.Personas.Count)].Id;
                var que = (Parentesco)rnd.Next(4);
                string? union = que == Parentesco.Hijo ? a.UnionesComoPareja(otro).Select(u => u.Id).FirstOrDefault() : null;
                var antes = ArbolGenealogico.Core.Io.ArbolJson.Serializar(a);
                bool conElla = rnd.Next(2) == 0;
                var (r, error) = a.ProbarMover(p.Id, v, que, otro, union, conElla);
                if (error != null)
                {
                    Assert.Equal(antes, ArbolGenealogico.Core.Io.ArbolJson.Serializar(a));
                    continue;
                }
                var r2 = a.Mover(p.Id, v, que, otro, union, conElla);
                Assert.Equal(r!.Quitadas.Count, r2.Quitadas.Count);
                Assert.NotNull(a.Buscar(p.Id));
                Coherente(a);
                hechos++;
            }
        }
        Assert.True(hechos > 100, $"Solo {hechos} movimientos");
    }
}
