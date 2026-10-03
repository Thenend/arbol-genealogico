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
        a.Obtener("p2").ArbolEnlazado = "otro/arbol.json";
        var json = ArbolJson.Serializar(a);
        var b = ArbolJson.Deserializar(json, out var avisos);
        Assert.Empty(avisos);
        Assert.Equal(a.Personas.Count, b.Personas.Count);
        Assert.Equal(a.Uniones.Count, b.Uniones.Count);
        Assert.Equal("Nació en Ávila.\nSegunda línea.", b.Obtener(b.RaizId).Historia);
        Assert.Equal("otro/arbol.json", b.Obtener("p2").ArbolEnlazado);
        Assert.Equal(Sexo.Mujer, b.Obtener("p3").Sexo);
        Assert.Contains("\"sexo\": \"M\"", json);
        Assert.Contains("ñ".Length == 1 ? "Ávila" : "", json);   // sin escapes Á
    }

    [Fact]
    public void Reparar_descarta_referencias_rotas()
    {
        var json = """{"raizId":"zz","personas":[{"id":"a"},{"id":"a"},{"id":"b"}],"uniones":[{"id":"u","parejas":["a","x"],"hijos":["b","q"]}]}""";
        var a = ArbolJson.Deserializar(json, out var avisos);
        Assert.Equal(2, a.Personas.Count);
        Assert.Equal(new[] { "a" }, a.Uniones[0].Parejas);
        Assert.Equal(new[] { "b" }, a.Uniones[0].Hijos);
        Assert.Equal("a", a.RaizId);
        Assert.NotEmpty(avisos);
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
        var quitadas = a.Eliminar("p2");   // el padre: sus ancestros quedan desconectados
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
        Assert.Contains("p2", d);       // padre
        Assert.DoesNotContain(a.Personas.First(p => p.Nombre == "Tío Juan").Id, d);
    }
}
