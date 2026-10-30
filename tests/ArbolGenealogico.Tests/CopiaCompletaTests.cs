using System.Text;
using ArbolGenealogico.Core.Io;

namespace ArbolGenealogico.Tests;

public class CopiaCompletaTests
{
    [Fact]
    public void Guarda_varios_arboles_y_los_devuelve_tal_cual()
    {
        var a = Familias.Tipica();
        var b = Familias.Tipica();
        b.Nombre = "Otra familia";
        var fecha = new DateTime(2026, 10, 30, 23, 5, 0);
        var datos = CopiaCompleta.Crear(new[] { ("tipica.ramo", ArbolJson.Serializar(a)), ("otra familia.ramo", ArbolJson.Serializar(b)) }, fecha);

        Assert.True(CopiaCompleta.Es(datos));
        var copia = CopiaCompleta.Leer(datos);
        Assert.Equal(fecha, copia.Fecha);
        Assert.Equal(new[] { "tipica.ramo", "otra familia.ramo" }, copia.Arboles.Select(x => x.Archivo));
        // cada árbol se lee como si viniera de su propio archivo, con las mismas personas y sus fotos
        var a2 = ArbolJson.Deserializar(copia.Arboles[0].Contenido, out _);
        Assert.Equal(ArbolJson.Serializar(a), ArbolJson.Serializar(a2));
        Assert.Equal("Otra familia", ArbolJson.Deserializar(copia.Arboles[1].Contenido, out _).Nombre);
    }

    [Fact]
    public void Un_arbol_no_es_una_copia_completa_ni_al_reves()
    {
        var arbol = Encoding.UTF8.GetBytes(ArbolJson.Serializar(Familias.Tipica()));
        Assert.False(CopiaCompleta.Es(arbol));
        Assert.Throws<InvalidDataException>(() => CopiaCompleta.Leer(arbol));

        // una versión de Ramo sin copia completa no la toma por un árbol: dice que no lo es
        var copia = CopiaCompleta.Crear(new[] { ("tipica.ramo", ArbolJson.Serializar(Familias.Tipica())) }, DateTime.Now);
        Assert.Throws<InvalidDataException>(() => ArbolJson.Deserializar(Encoding.UTF8.GetString(copia), out _));
    }

    [Fact]
    public void Un_archivo_danado_no_entra_y_los_demas_si()
    {
        var bueno = ArbolJson.Serializar(Familias.Tipica());
        var datos = CopiaCompleta.Crear(new[] { ("roto.ramo", "{\"personas\": [ {\"id\": "), ("bueno.ramo", bueno) }, DateTime.Now);
        var copia = CopiaCompleta.Leer(datos);
        Assert.Equal("bueno.ramo", Assert.Single(copia.Arboles).Archivo);
    }

    [Fact]
    public void Sin_arboles_es_una_copia_vacia()
    {
        var copia = CopiaCompleta.Leer(CopiaCompleta.Crear(Array.Empty<(string, string)>(), DateTime.Now));
        Assert.Empty(copia.Arboles);
    }
}
