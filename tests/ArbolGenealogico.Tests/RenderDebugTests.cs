using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

/// <summary>Genera PNGs en la carpeta indicada por LAYOUT_OUT (si no está definida, no hace nada).</summary>
public class RenderDebugTests
{
    [Fact]
    public void Render()
    {
        var dir = Environment.GetEnvironmentVariable("LAYOUT_OUT");
        if (string.IsNullOrEmpty(dir)) return;
        void R(string nombre, Arbol a)
        {
            var r = LayoutEngine.Calcular(a);
            Dibujo.Guardar(a, r, Path.Combine(dir, nombre + ".png"));
            File.WriteAllText(Path.Combine(dir, nombre + ".txt"), Verificacion.Comprobar(r).ToString());
        }
        R("tipica", Familias.Tipica());
        R("segundas", Familias.SegundasParejas());
        var semillas = (Environment.GetEnvironmentVariable("LAYOUT_SEEDS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in semillas)
        {
            var parts = s.Split(':');
            R("sem" + parts[0], Familias.Aleatoria(int.Parse(parts[0]), int.Parse(parts[1]), parts.Length > 2 ? int.Parse(parts[2]) : 3));
        }
    }

    /// <summary>Genera los JSON de ejemplo (solo si EJEMPLOS_OUT está definida).</summary>
    [Fact]
    public void GenerarEjemplos()
    {
        var dir = Environment.GetEnvironmentVariable("EJEMPLOS_OUT");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);

        var garcia = Familias.Tipica();
        garcia.Nombre = "Familia García";
        garcia.Obtener(garcia.RaizId).Historia = "Nació en Ávila en 1985. Estudió ingeniería y le encanta cocinar los domingos para toda la familia.";
        garcia.Personas.First(p => p.Nombre == "Abuelo paterno").Historia = "Zapatero de oficio. Montó su taller en 1952 y lo mantuvo abierto hasta jubilarse.";
        var rosa = garcia.Personas.First(p => p.Nombre == "Tía Rosa");
        rosa.Historia = "Se casó con el tío Juan en 1980. Su familia vive en su propio árbol (pulsa el icono de enlace de su tarjeta).";
        rosa.ArbolEnlazado = "familia-perez.json";
        File.WriteAllText(Path.Combine(dir, "familia-garcia.json"), Core.Io.ArbolJson.Serializar(garcia));

        var perez = Arbol.Nuevo("Familia Pérez", "Rosa");
        perez.Obtener(perez.RaizId).Apellidos = "Pérez Ortega";
        perez.Obtener(perez.RaizId).Sexo = Sexo.Mujer;
        perez.Obtener(perez.RaizId).Historia = "Rosa es la tía Rosa del árbol de la familia García. Aquí está su propia familia.";
        var pa = perez.AnadirPadres(perez.RaizId);
        perez.Nombrar(pa[0].Id, "Ramón", "Pérez", Sexo.Hombre); perez.Nombrar(pa[1].Id, "Carmen", "Ortega", Sexo.Mujer);
        var h1 = perez.AnadirHermano(perez.RaizId).hermano; perez.Nombrar(h1.Id, "Álvaro", "Pérez Ortega", Sexo.Hombre);
        var h2 = perez.AnadirHermano(h1.Id).hermano; perez.Nombrar(h2.Id, "Inés", "Pérez Ortega", Sexo.Mujer);
        var e1 = perez.AnadirPareja(h1.Id); perez.Nombrar(e1.Id, "Silvia", "Lara", Sexo.Mujer);
        var s1 = perez.AnadirHijo(h1.Id); perez.Nombrar(s1.Id, "Pablo", "Pérez Lara", Sexo.Hombre);
        var abu = perez.AnadirPadres(pa[1].Id);
        perez.Nombrar(abu[0].Id, "Teodoro", "Ortega", Sexo.Hombre); perez.Nombrar(abu[1].Id, "Pilar", "Sáez", Sexo.Mujer);
        var tio = perez.AnadirHermano(pa[1].Id).hermano; perez.Nombrar(tio.Id, "Tío Luis", "Ortega Sáez", Sexo.Hombre);
        File.WriteAllText(Path.Combine(dir, "familia-perez.json"), Core.Io.ArbolJson.Serializar(perez));
    }
}
