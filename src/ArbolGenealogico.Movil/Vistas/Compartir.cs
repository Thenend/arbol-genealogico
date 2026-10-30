using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Movil.Dibujo;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>Lo que sale del teléfono: árboles (.ramo), PDF, imágenes y GEDCOM, siempre como archivos y por las aplicaciones del sistema.</summary>
public static class Compartir
{
    /// <summary>El tipo con el que se mandan los .ramo: WhatsApp y el correo los tratan como documento.</summary>
    public const string TipoArbol = "application/octet-stream";

    /// <summary>Ramo en Google Play: quien recibe un .ramo sin tener la aplicación sabe de dónde viene y cómo abrirlo.</summary>
    public const string EnlacePlay = "https://play.google.com/store/apps/details?id=com.thenend.arbolgenealogico";

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
            var solo = new Opcion(T("Solo este árbol"), Ui.Iconos.Compartir);
            var todos = new Opcion(N(enlazados.Count, "Este y su árbol enlazado", "Este y sus {0} árboles enlazados"), Ui.Iconos.Enlace,
                string.Join(", ", enlazados.Select(Path.GetFileNameWithoutExtension)));
            var r = await Principal.Actual.Menu(T("Algunas tarjetas enlazan con otros árboles"), new[] { solo, todos });
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
        // los .ramo solo se abren con Ramo: va un mensaje corto con el enlace (los PDF y las imágenes se ven en cualquier
        // teléfono y ya llevan «Hecho con Ramo» en la versión gratuita, así que van sin texto)
        var texto = T("Te envío nuestro árbol familiar, hecho con Ramo. Para abrirlo, descarga la aplicación gratis: {0}", EnlacePlay);
        await App.Plataforma.Compartir(copias, TipoArbol, T("Compartir «{0}»", nombre), texto);
    }

    /// <summary>Exporta el árbol a GEDCOM (para otros programas de genealogía) y lo manda con «Compartir».</summary>
    public static async Task Gedcom(string ruta, string nombre)
    {
        var a = ArbolJson.Cargar(ruta, out _);
        var archivo = Biblioteca.Limpiar(nombre) + Core.Io.Gedcom.Extension;
        var destino = App.Biblioteca.ArchivoParaCompartir(nombre, Core.Io.Gedcom.Extension);
        await File.WriteAllBytesAsync(destino, Core.Io.Gedcom.Exportar(a, archivo));
        await App.Plataforma.Compartir(new[] { destino }, TipoArbol, T("Exportar «{0}» a GEDCOM", nombre));
    }

    /// <summary>Guarda una copia del árbol donde elija la persona (Descargas, Drive…).</summary>
    public static async Task GuardarCopia(Control desde, string ruta, string nombre)
    {
        var almacen = TopLevel.GetTopLevel(desde)?.StorageProvider;
        if (almacen == null) return;
        // sin DefaultExtension: Avalonia en Android la añade al nombre propuesto aunque ya la lleve («Lazaro.ramo.ramo»)
        var archivo = await almacen.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = T("Guardar una copia"), SuggestedFileName = Biblioteca.Limpiar(nombre) + ArbolJson.Extension,
            FileTypeChoices = new[] { new FilePickerFileType(T("Árbol genealógico")) { Patterns = new[] { "*.ramo" }, MimeTypes = new[] { TipoArbol } } },
        });
        if (archivo == null) return;
        await using (var s = await archivo.OpenWriteAsync())
        await using (var f = File.OpenRead(ruta))
            await f.CopyToAsync(s);
        Principal.Actual.Aviso(T("Copia guardada"));
    }

    /// <summary>Comparte el árbol entero como PDF (vectorial: se lee ampliándolo aunque el árbol sea muy grande).</summary>
    public static async Task Pdf(LienzoArbol lienzo, string nombre)
    {
        var destino = App.Biblioteca.ArchivoParaCompartir(nombre, ".pdf");
        Principal.Actual.Aviso(T("Preparando el PDF…"));
        await Task.Yield();
        await lienzo.GuardarPdf(destino, nombre, Licencia.ConMarca);
        await App.Plataforma.Compartir(new[] { destino }, "application/pdf", T("Compartir el PDF de «{0}»", nombre));
    }

    /// <summary>Comparte el árbol entero como imagen PNG.</summary>
    public static async Task Imagen(LienzoArbol lienzo, string nombre)
    {
        var destino = App.Biblioteca.ArchivoParaCompartir(nombre, ".png");
        Principal.Actual.Aviso(T("Preparando la imagen…"));
        await Task.Yield();
        using (var img = lienzo.Imagen(1.5, Licencia.ConMarca)) img.Save(destino);
        await App.Plataforma.Compartir(new[] { destino }, "image/png", T("Compartir la imagen de «{0}»", nombre));
    }
}
