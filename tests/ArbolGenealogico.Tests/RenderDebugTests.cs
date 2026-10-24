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

    /// <summary>
    /// Todos los árboles de la carpeta Ejemplos (también los personales, que no se suben al repositorio) se colocan sin
    /// solapes ni cruces en las tres ordenaciones.
    /// </summary>
    [Fact]
    public void Ejemplos_sin_cruces_ni_solapes()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ArbolGenealogico.slnx"))) dir = Path.GetDirectoryName(dir);
        if (dir == null || !Directory.Exists(Path.Combine(dir, "Ejemplos"))) return;
        var fallos = new List<string>();
        foreach (var ruta in Directory.GetFiles(Path.Combine(dir, "Ejemplos")).Where(Core.Io.ArbolJson.EsArchivoDeArbol))
        {
            var a = Core.Io.ArbolJson.Cargar(ruta, out _);
            foreach (var o in Enum.GetValues<Ordenacion>())
            {
                var inf = Verificacion.Comprobar(LayoutEngine.Calcular(a, new LayoutOptions { Ordenacion = o }));
                if (!inf.Limpio) fallos.Add($"{Path.GetFileName(ruta)} ({o}): {inf}");
            }
        }
        Assert.True(fallos.Count == 0, string.Join("\n", fallos));
    }

    /// <summary>
    /// En los árboles de ejemplo, con todas las formas de tarjeta, la bajada hacia el hijo que está casi debajo del punto de
    /// unión de sus padres cae recta, sin un escalón de unos pocos píxeles.
    /// </summary>
    [Fact]
    public void Ejemplos_bajadas_sin_escalones()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "ArbolGenealogico.slnx"))) dir = Path.GetDirectoryName(dir);
        if (dir == null || !Directory.Exists(Path.Combine(dir, "Ejemplos"))) return;
        var formas = new[] { "", "vertical", "sin-foto", "sin-tarjeta", "vertical sin-tarjeta" }.Select(Core.Presentacion.FormaTarjeta.Leer);
        var fallos = new List<string>();
        foreach (var ruta in Directory.GetFiles(Path.Combine(dir, "Ejemplos")).Where(Core.Io.ArbolJson.EsArchivoDeArbol))
        {
            var a = Core.Io.ArbolJson.Cargar(ruta, out _);
            foreach (var f in formas)
                foreach (var o in Enum.GetValues<Ordenacion>())
                    foreach (var c in LayoutEngine.Calcular(a, f.Opciones(o)).Conexiones.Where(c => c.Tipo == TipoConexion.Descendencia))
                    {
                        double dx = Math.Abs(c.Puntos[^1].X - c.Puntos[0].X);
                        if (dx > 0.5 && dx < 12) fallos.Add($"{Path.GetFileName(ruta)} ({f}, {o}): {c.HijoId} {dx:F1}");
                    }
        }
        Assert.True(fallos.Count == 0, string.Join("\n", fallos));
    }

    /// <summary>Genera los árboles de ejemplo (solo si EJEMPLOS_OUT está definida).</summary>
    [Fact]
    public void GenerarEjemplos()
    {
        var dir = Environment.GetEnvironmentVariable("EJEMPLOS_OUT");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);

        var garcia = Familias.Tipica();
        garcia.Nombre = "Familia García";
        // nombres de verdad en vez de los parentescos de Familias.Tipica (y el sexo de los que no lo tienen)
        var nombres = new Dictionary<string, (string Nombre, Sexo? Sexo)>
        {
            ["Yo"] = ("Javier", null), ["Padre"] = ("Antonio", null), ["Madre"] = ("Mercedes", null), ["Hermana"] = ("Beatriz", null),
            ["Hermano menor"] = ("Sergio", null), ["Abuelo paterno"] = ("Manuel", null), ["Abuela paterna"] = ("Dolores", null),
            ["Tío Juan"] = ("Juan", null), ["Tía Marta"] = ("Marta", null), ["Tía Rosa"] = ("Rosa", null),
            ["Primo 1"] = ("Óscar", Sexo.Hombre), ["Primo 2"] = ("Cristina", Sexo.Mujer), ["Primo 3"] = ("Raúl", Sexo.Hombre),
            ["Sobrino segundo"] = ("Hugo", Sexo.Hombre), ["Tío Pedro"] = ("Pedro", null), ["Prima Lucía"] = ("Lucía", null),
            ["Abuelo materno"] = ("Francisco", null), ["Abuela materna"] = ("Josefa", null), ["Tía Elena"] = ("Elena", null),
            ["Primo materno 1"] = ("Diego", Sexo.Hombre), ["Primo materno 2"] = ("Paula", Sexo.Mujer), ["Bisabuelo"] = ("Eusebio", null),
            ["Bisabuela"] = ("Remedios", null), ["Mi esposa"] = ("Laura", null), ["Hijo 1"] = ("Daniel", Sexo.Hombre),
            ["Hijo 2"] = ("Adrián", Sexo.Hombre), ["Nieto"] = ("Leo", Sexo.Hombre), ["Cuñado"] = ("Carlos", null),
            ["Sobrino 1"] = ("Iván", Sexo.Hombre), ["Sobrino 2"] = ("Claudia", Sexo.Mujer), ["Sobrino 3"] = ("Nora", Sexo.Mujer),
        };
        foreach (var p in garcia.Personas)
            if (nombres.TryGetValue(p.Nombre, out var n)) { p.Nombre = n.Nombre; p.Sexo = n.Sexo ?? p.Sexo; }
        garcia.Obtener(garcia.RaizId).Historia = "Nació en Ávila en 1985. Estudió ingeniería y le encanta cocinar los domingos para toda la familia.";
        garcia.Personas.First(p => p.Nombre == "Manuel").Historia = "Zapatero de oficio. Montó su taller en 1952 y lo mantuvo abierto hasta jubilarse.";
        var rosa = garcia.Personas.First(p => p.Nombre == "Rosa");
        rosa.Historia = "Se casó con Juan en 1980. Su familia vive en su propio árbol (pulsa el icono de enlace de su tarjeta).";
        rosa.ArbolEnlazado = "familia-perez.ramo";
        // los retratos de Ejemplos/fotos (los recorta tienda/promo/fotos-ejemplo.py), «Adrián» → adrian.jpg
        var raiz = AppContext.BaseDirectory;
        while (raiz != null && !File.Exists(Path.Combine(raiz, "ArbolGenealogico.slnx"))) raiz = Path.GetDirectoryName(raiz);
        string? Foto(string nombre)
        {
            var f = Path.Combine(raiz ?? "", "Ejemplos", "fotos", nombre.ToLowerInvariant().Replace('á', 'a').Replace('é', 'e')
                .Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u') + ".jpg");
            return File.Exists(f) ? Convert.ToBase64String(File.ReadAllBytes(f)) : null;
        }
        foreach (var p in garcia.Personas) p.Foto = Foto(p.Nombre);
        File.WriteAllText(Path.Combine(dir, "familia-garcia.ramo"), Core.Io.ArbolJson.Serializar(garcia));

        var perez = Arbol.Nuevo("Familia Pérez", "Rosa");
        perez.Obtener(perez.RaizId).Apellidos = "Pérez Ortega";
        perez.Obtener(perez.RaizId).Sexo = Sexo.Mujer;
        perez.Obtener(perez.RaizId).Historia = "Rosa es la tía de Javier en el árbol de la familia García. Aquí está su propia familia.";
        perez.Obtener(perez.RaizId).Foto = Foto("Rosa");
        var pa = perez.AnadirPadres(perez.RaizId);
        perez.Nombrar(pa[0].Id, "Ramón", "Pérez", Sexo.Hombre); perez.Nombrar(pa[1].Id, "Carmen", "Ortega", Sexo.Mujer);
        var h1 = perez.AnadirHermano(perez.RaizId).hermano; perez.Nombrar(h1.Id, "Álvaro", "Pérez Ortega", Sexo.Hombre);
        var h2 = perez.AnadirHermano(h1.Id).hermano; perez.Nombrar(h2.Id, "Inés", "Pérez Ortega", Sexo.Mujer);
        var e1 = perez.AnadirPareja(h1.Id); perez.Nombrar(e1.Id, "Silvia", "Lara", Sexo.Mujer);
        var s1 = perez.AnadirHijo(h1.Id); perez.Nombrar(s1.Id, "Pablo", "Pérez Lara", Sexo.Hombre);
        var abu = perez.AnadirPadres(pa[1].Id);
        perez.Nombrar(abu[0].Id, "Teodoro", "Ortega", Sexo.Hombre); perez.Nombrar(abu[1].Id, "Pilar", "Sáez", Sexo.Mujer);
        var tio = perez.AnadirHermano(pa[1].Id).hermano; perez.Nombrar(tio.Id, "Luis", "Ortega Sáez", Sexo.Hombre);
        File.WriteAllText(Path.Combine(dir, "familia-perez.ramo"), Core.Io.ArbolJson.Serializar(perez));
    }
}
