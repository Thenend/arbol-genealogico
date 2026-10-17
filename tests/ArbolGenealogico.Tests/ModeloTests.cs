using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

public class ModeloTests
{
    [Fact]
    public void Json_ida_y_vuelta_conserva_todo()
    {
        var a = Familias.Tipica();
        a.Obtener(a.RaizId).Foto = "AAAA";
        a.Obtener(a.RaizId).Historia = "Nació en Ávila.\nSegunda línea.";
        a.Obtener(a.IdDe("Padre")).ArbolEnlazado = "otro/arbol.json";
        var json = ArbolJson.Serializar(a);
        var b = ArbolJson.Deserializar(json, out var avisos);
        Assert.Empty(avisos);
        Assert.Equal(a.Personas.Count, b.Personas.Count);
        Assert.Equal(a.Uniones.Count, b.Uniones.Count);
        Assert.Equal("Nació en Ávila.\nSegunda línea.", b.Obtener(b.RaizId).Historia);
        Assert.Equal("otro/arbol.json", b.Obtener(a.IdDe("Padre")).ArbolEnlazado);
        Assert.Equal(Sexo.Mujer, b.Obtener(a.IdDe("Madre")).Sexo);
        Assert.Contains("\"sexo\": \"M\"", json);
        Assert.Contains("ñ".Length == 1 ? "Ávila" : "", json);   // sin escapes Á
    }

    [Fact]
    public void Reparar_descarta_referencias_rotas()
    {
        var json = """{"raizId":"zz","personas":[{"id":"a","nombre":"A"},{"id":"a"},{"id":"b","nombre":"B"}],"uniones":[{"id":"u","parejas":["a","x"],"hijos":["b","q"]}]}""";
        var a = ArbolJson.Deserializar(json, out var avisos);
        Assert.Equal(2, a.Personas.Count);
        Assert.Equal(new[] { a.IdDe("A") }, a.Uniones[0].Parejas);
        Assert.Equal(new[] { a.IdDe("B") }, a.Uniones[0].Hijos);
        Assert.Equal(a.IdDe("A"), a.RaizId);
        Assert.NotEmpty(avisos);
    }

    [Fact]
    public void Ids_nuevos_son_globales_y_no_se_repiten_entre_arboles()
    {
        var a = Familias.Tipica();
        var b = Familias.Tipica();
        Assert.All(a.Personas, p => Assert.True(Arbol.EsIdGlobal(p.Id)));
        Assert.All(a.Uniones, u => Assert.True(Arbol.EsIdGlobal(u.Id)));
        Assert.Empty(a.Personas.Select(p => p.Id).Intersect(b.Personas.Select(p => p.Id)));
        Assert.Equal(Arbol.VersionActual, a.Version);
    }

    [Fact]
    public void Archivo_version_1_se_convierte_a_ids_globales_conservando_la_estructura()
    {
        const string v1 = """
            {"version":1,"nombre":"Vieja","raizId":"p1",
             "personas":[{"id":"p1","nombre":"Yo"},{"id":"p2","nombre":"Padre","sexo":"M"},{"id":"p3","nombre":"Madre","sexo":"F"}],
             "uniones":[{"id":"u1","parejas":["p2","p3"],"hijos":["p1"]}]}
            """;
        var a = ArbolJson.Deserializar(v1, out var avisos);
        Assert.Empty(avisos);
        Assert.Equal(Arbol.VersionActual, a.Version);
        Assert.All(a.Personas, p => Assert.True(Arbol.EsIdGlobal(p.Id)));
        Assert.True(Arbol.EsIdGlobal(a.Uniones[0].Id));
        Assert.Equal(a.IdDe("Yo"), a.RaizId);
        Assert.Equal(new[] { a.IdDe("Padre"), a.IdDe("Madre") }, a.Uniones[0].Parejas);
        Assert.Equal(new[] { a.IdDe("Yo") }, a.Uniones[0].Hijos);

        // Dos copias del mismo archivo antiguo (aunque cambien los espacios) dan los mismos ids: se reconocen al fusionar.
        var otraCopia = ArbolJson.Deserializar(v1.Replace(",", " ,\n"), out _);
        Assert.Equal(a.Personas.Select(p => p.Id), otraCopia.Personas.Select(p => p.Id));
        // Un árbol antiguo distinto con los mismos p1, p2… no comparte ninguno.
        var otro = ArbolJson.Deserializar(v1.Replace("\"Padre\"", "\"Papá\""), out _);
        Assert.Empty(a.Personas.Select(p => p.Id).Intersect(otro.Personas.Select(p => p.Id)));

        // Guardado y vuelto a abrir ya no cambia.
        var c = ArbolJson.Deserializar(ArbolJson.Serializar(a), out _);
        Assert.Equal(a.Personas.Select(p => p.Id), c.Personas.Select(p => p.Id));
        Assert.Equal(a.Uniones[0].Id, c.Uniones[0].Id);
    }

    [Fact]
    public void Archivo_de_version_mas_nueva_avisa()
    {
        ArbolJson.Deserializar("""{"version":99,"personas":[{"id":"a"}],"uniones":[]}""", out var avisos);
        Assert.Contains(avisos, x => x.Contains("más nueva"));
    }

    [Fact]
    public void No_es_un_arbol_da_error_claro()
    {
        Assert.Throws<InvalidDataException>(() => ArbolJson.Deserializar("""{"personas":null}""", out _));
    }

    [Fact]
    public void Politica_no_admite_padres_ni_hermanos()
    {
        var a = Arbol.Nuevo("x", "yo");
        var pareja = a.AnadirPareja(a.RaizId);
        Assert.False(a.EsSanguinea(pareja.Id));
        Assert.Throws<InvalidOperationException>(() => a.AnadirPadres(pareja.Id));
        Assert.Throws<InvalidOperationException>(() => a.AnadirHermano(pareja.Id));
    }

    [Fact]
    public void Hijo_con_pareja_desconocida_y_luego_pareja_completa_la_union()
    {
        var a = Arbol.Nuevo("x", "yo");
        var h = a.AnadirHijo(a.RaizId);
        Assert.Single(a.Uniones);
        Assert.Single(a.Uniones[0].Parejas);
        a.AnadirPareja(a.RaizId);
        Assert.Single(a.Uniones);
        Assert.Equal(2, a.Uniones[0].Parejas.Count);
        Assert.Equal(h.Id, a.Uniones[0].Hijos[0]);
    }

    [Fact]
    public void Hermano_sin_padres_crea_padres_y_se_inserta_tras_la_persona()
    {
        var a = Arbol.Nuevo("x", "yo");
        var (h, padres) = a.AnadirHermano(a.RaizId);
        Assert.Equal(2, padres.Count);
        var u = a.UnionComoHijo(a.RaizId)!;
        Assert.Equal(new[] { a.RaizId, h.Id }, u.Hijos);
    }

    [Fact]
    public void Eliminar_quita_lo_desconectado_y_protege_la_raiz()
    {
        var a = Familias.Tipica();
        Assert.Throws<InvalidOperationException>(() => a.Eliminar(a.RaizId));
        var antes = a.Personas.Count;
        // el tío Juan arrastra a su mujer solo si ella queda sin conexión; sus hijos la mantienen conectada
        var quitadas = a.Eliminar(a.IdDe("Padre"));   // el padre: sus ancestros quedan desconectados
        Assert.True(quitadas.Count > 1);
        Assert.Equal(antes - quitadas.Count, a.Personas.Count);
        Assert.Equal(a.Personas.Count, a.Alcanzables().Count);
    }

    [Fact]
    public void Linea_directa_son_los_antepasados()
    {
        var a = Familias.Tipica();
        var d = a.LineaDirecta();
        Assert.Contains(a.RaizId, d);
        Assert.Contains(a.IdDe("Padre"), d);       // padre
        Assert.DoesNotContain(a.Personas.First(p => p.Nombre == "Tío Juan").Id, d);
    }

    [Fact]
    public void Linaje_incluye_antepasados_y_descendientes_pero_no_ramas_laterales()
    {
        var a = Familias.Tipica();
        string Id(string nombre) => a.Personas.First(p => p.Nombre == nombre).Id;
        var (antepasados, descendientes) = a.Linaje(Id("Tío Juan"));
        Assert.Contains(Id("Abuelo paterno"), antepasados);
        Assert.Contains(Id("Abuela paterna"), antepasados);
        Assert.DoesNotContain(Id("Padre"), antepasados);        // su hermano no es antepasado
        Assert.DoesNotContain(Id("Tía Rosa"), antepasados);
        Assert.Contains(Id("Primo 2"), descendientes);
        Assert.Contains(Id("Sobrino segundo"), descendientes);   // nieto
        Assert.DoesNotContain(Id("Yo"), descendientes);
        Assert.DoesNotContain(Id("Tío Juan"), antepasados);
    }

    [Fact]
    public void FamiliaDirecta_incluye_linaje_pareja_y_hermanos_pero_no_tios_ni_primos()
    {
        var a = Familias.Tipica();
        string Id(string nombre) => a.Personas.First(p => p.Nombre == nombre).Id;
        var f = a.FamiliaDirecta(Id("Yo"));
        Assert.Contains(Id("Padre"), f);
        Assert.Contains(Id("Abuela materna"), f);
        Assert.Contains(Id("Bisabuelo"), f);
        Assert.Contains(Id("Hijo 1"), f);
        Assert.Contains(Id("Nieto"), f);
        Assert.Contains(Id("Mi esposa"), f);        // su pareja
        Assert.Contains(Id("Hermana"), f);          // sus hermanos
        Assert.Contains(Id("Hermano menor"), f);
        Assert.DoesNotContain(Id("Tío Juan"), f);   // tíos, primos y sobrinos quedan fuera
        Assert.DoesNotContain(Id("Primo 1"), f);
        Assert.DoesNotContain(Id("Sobrino 1"), f);
        Assert.DoesNotContain(Id("Cuñado"), f);
    }

    [Fact]
    public void BuscarPorNombre_ignora_mayusculas_y_tildes_y_acepta_varias_palabras()
    {
        var a = Familias.Tipica();
        Assert.Equal(a.Personas.Count(p => p.Apellidos.Contains("García")), a.BuscarPorNombre("garcia").Count);
        Assert.Contains(a.BuscarPorNombre("TIO juan"), p => p.Nombre == "Tío Juan");
        var uno = a.BuscarPorNombre("juan garcia ruiz");           // palabras en cualquier orden, repartidas entre nombre y apellidos
        Assert.Single(uno);
        Assert.Equal("Tío Juan", uno[0].Nombre);
        Assert.Empty(a.BuscarPorNombre("zzzz"));
        Assert.Empty(a.BuscarPorNombre(""));
        Assert.Empty(a.BuscarPorNombre("   "));
    }

    [Fact]
    public void Texto_Normalizar_quita_tildes_y_pasa_a_minusculas()
    {
        Assert.Equal("nandu aeiou", Core.Model.Texto.Normalizar("Ñandú ÁÉÍÓÚ"));
        Assert.Equal("", Core.Model.Texto.Normalizar(null));
    }

    private static HashSet<string> Nombres(Arbol a) => a.Personas.Select(p => p.Nombre).ToHashSet();

    [Fact]
    public void ExtraerFamiliaDe_un_politico_se_lleva_pareja_hijos_y_nietos()
    {
        var a = Familias.Tipica();
        string Id(string nombre) => a.Personas.First(p => p.Nombre == nombre).Id;
        var n = a.ExtraerFamiliaDe(Id("Tía Rosa"));
        Assert.Equal(Id("Tía Rosa"), n.RaizId);
        Assert.Equal(new HashSet<string> { "Tía Rosa", "Tío Juan", "Primo 1", "Primo 2", "Primo 3", "Sobrino segundo" }, Nombres(n));
        Assert.Equal(n.Personas.Count, n.Alcanzables().Count);
        Assert.Equal(2, n.Uniones.Count);                           // la de Rosa y Juan, y la de Primo 2 con su hijo
        var deRosa = n.UnionesComoPareja(Id("Tía Rosa")).Single();
        Assert.Equal(3, deRosa.Hijos.Count);
    }

    [Fact]
    public void ExtraerFamiliaDe_se_lleva_a_todos_los_familiares_de_sangre_y_sus_conyuges_pero_no_la_familia_politica()
    {
        var a = Familias.Tipica();
        string Id(string nombre) => a.Personas.First(p => p.Nombre == nombre).Id;
        var n = a.ExtraerFamiliaDe(Id("Prima Lucía"));          // hija de Tía Marta (García) y Tío Pedro (político)
        var nombres = Nombres(n);
        // sangre: por parte de su madre, los abuelos paternos de la familia García y todos sus descendientes
        foreach (var esperado in new[] { "Prima Lucía", "Tía Marta", "Abuelo paterno", "Abuela paterna", "Tío Juan", "Padre",
                                          "Primo 1", "Primo 2", "Primo 3", "Sobrino segundo", "Yo", "Hermana", "Hermano menor",
                                          "Hijo 1", "Hijo 2", "Nieto", "Sobrino 1", "Sobrino 2", "Sobrino 3" })
            Assert.Contains(esperado, nombres);
        // cónyuges de los familiares de sangre
        foreach (var conyuge in new[] { "Tío Pedro", "Tía Rosa", "Madre", "Mi esposa", "Cuñado" })
            Assert.Contains(conyuge, nombres);
        // la familia de la madre de "Yo" (los López) es política para Lucía: no se lleva
        foreach (var excluido in new[] { "Abuelo materno", "Abuela materna", "Bisabuelo", "Bisabuela", "Tía Elena", "Primo materno 1", "Primo materno 2" })
            Assert.DoesNotContain(excluido, nombres);
        Assert.Equal(n.Personas.Count, n.Alcanzables().Count);
        // los cónyuges no tienen padres en el árbol nuevo; la madre de "Yo" sigue siendo su madre
        Assert.Null(n.UnionComoHijo(Id("Madre")));
        Assert.False(n.EsSanguinea(Id("Madre")));
        Assert.Contains(Id("Yo"), n.UnionComoHijo(Id("Yo"))!.Hijos);
        Assert.Contains(Id("Madre"), n.UnionComoHijo(Id("Yo"))!.Parejas);
    }

    [Fact]
    public void ExtraerFamiliaDe_la_raiz_de_un_arbol_normal_se_lleva_todo()
    {
        var a = Familias.Tipica();                               // todos son de sangre de "Yo" o cónyuges de ellos
        var n = a.ExtraerFamiliaDe(a.RaizId);
        Assert.Equal(a.Personas.Count, n.Personas.Count);
        Assert.Equal(a.Uniones.Count, n.Uniones.Count);
    }

    [Fact]
    public void ExtraerFamiliaDe_es_una_copia_independiente()
    {
        var a = Familias.Tipica();
        var n = a.ExtraerFamiliaDe(a.RaizId);
        n.Obtener(a.RaizId).Nombre = "Cambiado";
        n.Uniones[0].Hijos.Clear();
        Assert.Equal("Yo", a.Obtener(a.RaizId).Nombre);
        Assert.NotEmpty(a.Uniones[0].Hijos);
        Assert.Equal(31, a.Personas.Count);
    }

    [Fact]
    public void ExtraerFamiliaDe_en_familias_aleatorias_da_siempre_un_arbol_valido()
    {
        for (int s = 1; s <= 120; s++)
        {
            var a = Familias.Aleatoria(s * 31337, 40);
            foreach (var p in a.Personas.OrderBy(p => (p.Id.GetHashCode() ^ s)).Take(4))
            {
                var n = a.ExtraerFamiliaDe(p.Id);
                Assert.Equal(p.Id, n.RaizId);
                Assert.Equal(n.Personas.Count, n.Alcanzables().Count);
                // todo el que tiene padres en el árbol nuevo es sanguíneo (la familia política no se mezcla)
                Assert.All(n.Personas.Where(q => n.UnionComoHijo(q.Id) != null), q => Assert.True(n.EsSanguinea(q.Id), $"semilla {s}: {q.Id}"));
                var r = Core.Layout.LayoutEngine.Calcular(n);
                Assert.Equal(0, Core.Layout.Verificacion.Comprobar(r).Solapes);
            }
        }
    }

    [Fact]
    public void Reparar_quita_el_enlace_de_la_persona_principal_pero_no_el_de_las_demas()
    {
        var json = """{"raizId":"a","personas":[{"id":"a","nombre":"A","arbolEnlazado":"origen.json"},{"id":"b","nombre":"B","arbolEnlazado":"suyo.json"}],"uniones":[{"id":"u","parejas":["a","b"],"hijos":[]}]}""";
        var a = ArbolJson.Deserializar(json, out _);
        Assert.Null(a.Obtener(a.IdDe("A")).ArbolEnlazado);
        Assert.Equal("suyo.json", a.Obtener(a.IdDe("B")).ArbolEnlazado);
    }
}
