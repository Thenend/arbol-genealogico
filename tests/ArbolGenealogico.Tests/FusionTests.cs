using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

public class FusionTests
{
    [Fact]
    public void Dos_copias_del_mismo_arbol_con_anadidos_distintos_se_juntan_sin_duplicados()
    {
        var original = Familias.Tipica();
        var mio = original.Clonar();
        var suyo = original.Clonar();
        var miHijo = mio.AnadirHijo(mio.IdDe("Hijo 1")); miHijo.Nombre = "Nieta mía";
        var (suPrimo, _) = suyo.AnadirHermano(suyo.IdDe("Primo 1")); suPrimo.Nombre = "Primo 4";
        var suPareja = suyo.AnadirPareja(suyo.IdDe("Primo 3")); suPareja.Nombre = "Mujer del primo 3";

        var inf = Fusion.Fusionar(mio, suyo);

        Assert.Equal(original.Personas.Count + 3, mio.Personas.Count);
        Assert.Equal(2, inf.Nuevas);
        Assert.Equal(original.Personas.Count, inf.Comunes);
        Assert.Equal(0, inf.Sueltas);
        Assert.Empty(inf.Avisos);
        Assert.Equal(mio.Personas.Count, mio.Alcanzables().Count);
        // el primo nuevo, justo detrás de su hermano, como en el otro árbol
        var hermanos = mio.UnionComoHijo(mio.IdDe("Primo 1"))!.Hijos;
        Assert.Equal(hermanos.IndexOf(mio.IdDe("Primo 1")) + 1, hermanos.IndexOf(mio.IdDe("Primo 4")));
        Assert.Contains(mio.IdDe("Mujer del primo 3"), mio.UnionesComoPareja(mio.IdDe("Primo 3")).SelectMany(u => u.Parejas));
        foreach (var o in Enum.GetValues<Ordenacion>())
            Assert.True(Verificacion.Comprobar(LayoutEngine.Calcular(mio, new LayoutOptions { Ordenacion = o })).Limpio);
    }

    [Fact]
    public void Fusionar_dos_veces_lo_mismo_no_cambia_nada()
    {
        var a = Familias.Tipica();
        var b = a.Clonar();
        b.AnadirHijo(b.IdDe("Nieto")).Nombre = "Bisnieto";
        Fusion.Fusionar(a, b);
        var json = ArbolJson.Serializar(a);
        var inf = Fusion.Fusionar(a, b);
        Assert.Equal(0, inf.Nuevas);
        Assert.Equal(0, inf.Completadas);
        Assert.Equal(json, ArbolJson.Serializar(a));
    }

    [Fact]
    public void Se_completan_los_datos_vacios_y_las_diferencias_respetan_la_preferencia()
    {
        var a = Familias.Tipica();
        var b = a.Clonar();
        var yo = a.RaizId;
        b.Obtener(yo).Foto = "Rk9UTw==";
        b.Obtener(b.IdDe("Padre")).Historia = "Carpintero.";
        b.Obtener(b.IdDe("Madre")).Apellidos = "López Sanz";        // en el mío, «López»

        var conservar = a.Clonar();
        var inf = Fusion.Fusionar(conservar, b);
        Assert.Equal(2, inf.Completadas);
        Assert.Single(inf.ConDiferencias);
        Assert.Equal("Rk9UTw==", conservar.Obtener(yo).Foto);
        Assert.Equal("Carpintero.", conservar.Obtener(conservar.IdDe("Padre")).Historia);
        Assert.Equal("López", conservar.Obtener(conservar.IdDe("Madre")).Apellidos);

        var preferir = a.Clonar();
        Fusion.Fusionar(preferir, b, preferirOtro: true);
        Assert.Equal("López Sanz", preferir.Obtener(preferir.IdDe("Madre")).Apellidos);
    }

    [Fact]
    public void Arboles_empezados_por_separado_se_unen_por_quien_es_quien()
    {
        // mi árbol: yo, mis padres y mi abuela paterna
        var mio = Arbol.Nuevo("Mío", "Lucía");
        mio.Obtener(mio.RaizId).Apellidos = "Martín Gil"; mio.Obtener(mio.RaizId).Sexo = Sexo.Mujer;
        var padres = mio.AnadirPadres(mio.RaizId);
        mio.Nombrar(padres[0].Id, "Andrés", "Martín Ruiz", Sexo.Hombre);
        mio.Nombrar(padres[1].Id, "Elena", "Gil", Sexo.Mujer);
        // el de mi primo: él, su madre (hermana de mi padre), mi padre y los abuelos
        var suyo = Arbol.Nuevo("Suyo", "Pablo");
        suyo.Obtener(suyo.RaizId).Apellidos = "Soto Martín";
        var sp = suyo.AnadirPadres(suyo.RaizId);
        suyo.Nombrar(sp[0].Id, "Jorge", "Soto", Sexo.Hombre);
        suyo.Nombrar(sp[1].Id, "Rosa", "Martín Ruiz", Sexo.Mujer);
        var abuelos = suyo.AnadirPadres(sp[1].Id);
        suyo.Nombrar(abuelos[0].Id, "Abuelo", "Martín", Sexo.Hombre);
        suyo.Nombrar(abuelos[1].Id, "Abuela", "Ruiz", Sexo.Mujer);
        var tio = suyo.AnadirHermano(sp[1].Id).hermano;
        suyo.Nombrar(tio.Id, "Andrés", "Martín Ruiz", Sexo.Hombre);

        // sin decir quién es quién no hay nada en común: lo nuevo quedaría suelto
        Assert.Equal(suyo.Personas.Count, Fusion.Probar(mio, suyo).Sueltas);

        var parecidos = Fusion.Parecidos(mio, suyo);
        var andres = Assert.Single(parecidos);
        Assert.Equal(mio.IdDe("Andrés"), andres.IdMio);
        Assert.Equal(tio.Id, andres.IdSuyo);

        var inf = Fusion.Fusionar(mio, suyo, parecidos.ToDictionary(p => p.IdSuyo, p => p.IdMio));
        Assert.Equal(0, inf.Sueltas);
        Assert.Equal(1, inf.Comunes);
        Assert.Equal(8, mio.Personas.Count);
        // mi padre tiene ahora a sus padres y a su hermana Rosa
        var deAndres = mio.UnionComoHijo(mio.IdDe("Andrés"))!;
        Assert.Contains(mio.IdDe("Rosa"), deAndres.Hijos);
        Assert.Contains(mio.IdDe("Abuelo"), deAndres.Parejas);
        Assert.True(Verificacion.Comprobar(LayoutEngine.Calcular(mio)).Limpio);
    }

    [Fact]
    public void Parecidos_no_empareja_sexos_contrarios_ni_nombres_distintos()
    {
        var mio = Arbol.Nuevo("a", "Alex"); mio.Obtener(mio.RaizId).Sexo = Sexo.Hombre;
        var suyo = Arbol.Nuevo("b", "Alex"); suyo.Obtener(suyo.RaizId).Sexo = Sexo.Mujer;
        Assert.Empty(Fusion.Parecidos(mio, suyo));
        suyo.Obtener(suyo.RaizId).Sexo = Sexo.Desconocido;
        Assert.Single(Fusion.Parecidos(mio, suyo));
        suyo.Obtener(suyo.RaizId).Nombre = "Álex";      // las tildes no cuentan
        Assert.Single(Fusion.Parecidos(mio, suyo));
        suyo.Obtener(suyo.RaizId).Nombre = "Alejandro";
        Assert.Empty(Fusion.Parecidos(mio, suyo));
    }

    [Fact]
    public void Un_hijo_con_padres_distintos_en_cada_arbol_conserva_los_de_este_y_avisa()
    {
        var a = Familias.Tipica();
        var b = a.Clonar();
        // en el otro árbol, «Hermana» cuelga de una pareja nueva de la madre
        var hermana = b.IdDe("Hermana");
        b.UnionComoHijo(hermana)!.Hijos.Remove(hermana);
        var otroMarido = b.AnadirPareja(b.IdDe("Madre"));
        b.UnionesComoPareja(otroMarido.Id).Single().Hijos.Add(hermana);

        var inf = Fusion.Fusionar(a, b);
        Assert.Contains(inf.Avisos, x => x.Contains("Hermana"));
        Assert.Equal(a.IdDe("Padre"), a.UnionComoHijo(hermana)!.Parejas.First(p => a.Obtener(p).Sexo == Sexo.Hombre));
    }

    [Fact]
    public void Fusiones_aleatorias_dejan_arboles_validos()
    {
        for (int s = 1; s <= 40; s++)
        {
            var baseA = Familias.Aleatoria(s, 25);
            var mio = baseA.Clonar(); var suyo = baseA.Clonar();
            var rnd = new Random(s);
            for (int i = 0; i < 6; i++)
            {
                var a = i % 2 == 0 ? mio : suyo;
                var p = a.Personas[rnd.Next(a.Personas.Count)];
                if (rnd.Next(2) == 0) a.AnadirPareja(p.Id); else a.AnadirHijo(p.Id, a.UnionesComoPareja(p.Id).FirstOrDefault()?.Id);
            }
            int esperado = baseA.Personas.Count + (mio.Personas.Count - baseA.Personas.Count) + (suyo.Personas.Count - baseA.Personas.Count);
            var inf = Fusion.Fusionar(mio, suyo);
            Assert.Equal(esperado, mio.Personas.Count);
            Assert.Equal(0, inf.Sueltas);
            Assert.Empty(mio.Reparar());
            var r = LayoutEngine.Calcular(mio);
            Assert.Equal(0, Verificacion.Comprobar(r).Solapes);
        }
    }
}

public class FusionEjemplosTests
{
    [Fact]
    public void La_familia_de_una_pareja_politica_se_puede_fusionar_y_se_coloca_sin_solapes()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ArbolGenealogico.slnx"))) dir = Path.GetDirectoryName(dir);
        if (dir == null) return;
        var garcia = ArbolJson.Cargar(Path.Combine(dir, "Ejemplos", "familia-garcia.ramo"), out _);
        var perez = ArbolJson.Cargar(Path.Combine(dir, "Ejemplos", "familia-perez.ramo"), out _);
        var rosaG = garcia.IdDe("Tía Rosa");
        var inf = Fusion.Fusionar(garcia, perez, new Dictionary<string, string> { [perez.RaizId] = rosaG });
        Assert.Equal(0, inf.Sueltas);
        Assert.Equal(9, inf.Nuevas);
        foreach (var o in Enum.GetValues<Ordenacion>())
        {
            var r = LayoutEngine.Calcular(garcia, new LayoutOptions { Ordenacion = o });
            Assert.Equal(garcia.Personas.Count, r.Cartas.Count);
            Assert.Equal(0, Verificacion.Comprobar(r).Solapes);
        }
    }

    [Fact]
    public void Un_enlace_a_un_tree_lleva_al_mismo_arbol_ya_como_ramo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ramo-enlace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var origen = Path.Combine(dir, "a.ramo");
            File.WriteAllText(Path.Combine(dir, "familia-perez.ramo"), "{}");
            Assert.Equal(Path.Combine(dir, "familia-perez.ramo"), ArbolJson.ResolverRuta(origen, "familia-perez.tree"));
            Assert.Equal(Path.Combine(dir, "familia-perez.ramo"), ArbolJson.ResolverRuta(origen, "familia-perez.ramo"));
            // si no está con ninguna extensión, la ruta tal cual (para avisar de que no se encuentra)
            Assert.Equal(Path.Combine(dir, "otra.tree"), ArbolJson.ResolverRuta(origen, "otra.tree"));
            Assert.True(ArbolJson.EsArchivoDeArbol("x.ramo") && ArbolJson.EsArchivoDeArbol("x.TREE") && ArbolJson.EsArchivoDeArbol("x.json"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
