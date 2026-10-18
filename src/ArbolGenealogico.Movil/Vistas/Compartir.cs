using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Movil.Dibujo;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>Lo que sale del teléfono: árboles (.ramo), PDF, imágenes y GEDCOM, siempre como archivos y por las aplicaciones del sistema.</summary>
public static class Compartir
{
    /// <summary>El tipo con el que se mandan los .ramo: WhatsApp y el correo los tratan como documento.</summary>
    public const string TipoArbol = "application/octet-stream";

    /// <summary>Comparte un árbol y, si se quiere, los árboles enlazados desde sus tarjetas que estén en el teléfono.</summary>
    public static async Task Arbol(string ruta, string nombre)
    {
        var a = ArbolJson.Cargar(ruta, out _);
        var enlazados = a.Personas.Select(p => App.Biblioteca.Resolver(p.ArbolEnlazado))
            .Where(r => r != null && !string.Equals(r, ruta, StringComparison.OrdinalIgnoreCase))
            .Select(r => r!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var rutas = new List<string> { ruta };
        if (enlazados.Count > 0)
        {
            var solo = new Opcion("Solo este árbol", Ui.Iconos.Compartir);
            var todos = new Opcion(enlazados.Count == 1 ? "Este y su árbol enlazado" : $"Este y sus {enlazados.Count} árboles enlazados", Ui.Iconos.Enlace,
                string.Join(", ", enlazados.Select(Path.GetFileNameWithoutExtension)));
            var r = await Principal.Actual.Menu("Algunas tarjetas enlazan con otros árboles", new[] { solo, todos });
            if (r == null) return;
            if (r == todos) rutas.AddRange(enlazados);
        }
        var copias = new List<string>();
        foreach (var origen in rutas)
        {
            // se manda con el nombre del archivo (es el que usan los enlaces entre árboles) y siempre como .ramo
            var destino = App.Biblioteca.ArchivoParaCompartir(Path.GetFileNameWithoutExtension(origen), ArbolJson.Extension);
            File.Copy(origen, destino, overwrite: true);
            copias.Add(destino);
        }
        await App.Plataforma.Compartir(copias, TipoArbol, "Compartir «" + nombre + "»");
    }

    /// <summary>Exporta el árbol a GEDCOM (para otros programas de genealogía) y lo manda con «Compartir».</summary>
    public static async Task Gedcom(string ruta, string nombre)
    {
        var a = ArbolJson.Cargar(ruta, out _);
        var archivo = Biblioteca.Limpiar(nombre) + Core.Io.Gedcom.Extension;
        var destino = App.Biblioteca.ArchivoParaCompartir(nombre, Core.Io.Gedcom.Extension);
        await File.WriteAllBytesAsync(destino, Core.Io.Gedcom.Exportar(a, archivo));
        await App.Plataforma.Compartir(new[] { destino }, TipoArbol, "Exportar «" + nombre + "» a GEDCOM");
    }

    /// <summary>Guarda una copia del árbol donde elija la persona (Descargas, Drive…).</summary>
    public static async Task GuardarCopia(Control desde, string ruta, string nombre)
    {
        var almacen = TopLevel.GetTopLevel(desde)?.StorageProvider;
        if (almacen == null) return;
        var archivo = await almacen.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar una copia", SuggestedFileName = Biblioteca.Limpiar(nombre) + ArbolJson.Extension, DefaultExtension = "ramo",
            FileTypeChoices = new[] { new FilePickerFileType("Árbol genealógico") { Patterns = new[] { "*.ramo" }, MimeTypes = new[] { TipoArbol } } },
        });
        if (archivo == null) return;
        await using (var s = await archivo.OpenWriteAsync())
        await using (var f = File.OpenRead(ruta))
            await f.CopyToAsync(s);
        Principal.Actual.Aviso("Copia guardada");
    }

    /// <summary>Comparte el árbol entero como PDF (vectorial: se lee ampliándolo aunque el árbol sea muy grande).</summary>
    public static async Task Pdf(LienzoArbol lienzo, string nombre)
    {
        var destino = App.Biblioteca.ArchivoParaCompartir(nombre, ".pdf");
        Principal.Actual.Aviso("Preparando el PDF…");
        await Task.Yield();
        await lienzo.GuardarPdf(destino, nombre);
        await App.Plataforma.Compartir(new[] { destino }, "application/pdf", "Compartir el PDF de «" + nombre + "»");
    }

    /// <summary>Comparte el árbol entero como imagen PNG.</summary>
    public static async Task Imagen(LienzoArbol lienzo, string nombre)
    {
        var destino = App.Biblioteca.ArchivoParaCompartir(nombre, ".png");
        Principal.Actual.Aviso("Preparando la imagen…");
        await Task.Yield();
        using (var img = lienzo.Imagen(1.5)) img.Save(destino);
        await App.Plataforma.Compartir(new[] { destino }, "image/png", "Compartir la imagen de «" + nombre + "»");
    }
}
