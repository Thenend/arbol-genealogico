using System.Text.Json;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Movil;

/// <summary>Un árbol de la biblioteca: su archivo, su nombre, cuántas personas tiene y los ids de todas ellas.</summary>
public sealed record EntradaArbol(string Ruta, string Nombre, int Personas, DateTime Fecha, string RaizId, HashSet<string> Ids)
{
    public string Archivo => Path.GetFileName(Ruta);
}

/// <summary>
/// Los árboles guardados en el teléfono: una carpeta privada de la aplicación con un archivo .tree por árbol. No hay
/// servidor: lo que entra llega como archivo (de WhatsApp, del correo…) y lo que sale se comparte como archivo.
/// </summary>
public sealed class Biblioteca
{
    public string Carpeta { get; }
    public string CarpetaTemporal { get; }
    private readonly Dictionary<string, (DateTime Fecha, long Tamano, EntradaArbol Entrada)> _indice = new(StringComparer.OrdinalIgnoreCase);

    public Biblioteca(string carpetaDatos)
    {
        Carpeta = Path.Combine(carpetaDatos, "Arboles");
        CarpetaTemporal = Path.Combine(carpetaDatos, "Temporal");
        Directory.CreateDirectory(Carpeta);
    }

    public List<EntradaArbol> Listar()
    {
        var lista = new List<EntradaArbol>();
        foreach (var ruta in Directory.EnumerateFiles(Carpeta).Where(ArbolJson.EsArchivoDeArbol))
            if (Leer(ruta) is { } e) lista.Add(e);
        return lista.OrderByDescending(e => e.Fecha).ToList();
    }

    /// <summary>Lo básico de un árbol sin cargarlo entero (ni sus fotos); se recuerda mientras el archivo no cambie.</summary>
    private EntradaArbol? Leer(string ruta)
    {
        try
        {
            var info = new FileInfo(ruta);
            if (_indice.TryGetValue(ruta, out var c) && c.Fecha == info.LastWriteTimeUtc && c.Tamano == info.Length) return c.Entrada;
            using var doc = JsonDocument.Parse(File.ReadAllBytes(ruta), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var raiz = doc.RootElement;
            string Texto(string nombre) => raiz.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var ids = new HashSet<string>();
            if (raiz.TryGetProperty("personas", out var ps) && ps.ValueKind == JsonValueKind.Array)
                foreach (var p in ps.EnumerateArray())
                    if (p.TryGetProperty("id", out var id) && id.GetString() is { } s) ids.Add(s);
            var nombre = Texto("nombre");
            if (string.IsNullOrWhiteSpace(nombre)) nombre = Path.GetFileNameWithoutExtension(ruta);
            var e = new EntradaArbol(ruta, nombre, ids.Count, info.LastWriteTime, Texto("raizId"), ids);
            _indice[ruta] = (info.LastWriteTimeUtc, info.Length, e);
            return e;
        }
        catch { return null; }
    }

    public Arbol Cargar(string ruta, out List<string> avisos) => ArbolJson.Cargar(ruta, out avisos);

    public void Guardar(string ruta, Arbol a) => ArbolJson.Guardar(ruta, a);

    /// <summary>Guarda un árbol nuevo con un nombre de archivo libre a partir de <paramref name="nombre"/>.</summary>
    public string GuardarNuevo(Arbol a, string nombre)
    {
        var ruta = RutaLibre(nombre);
        ArbolJson.Guardar(ruta, a);
        return ruta;
    }

    /// <summary>
    /// Copia en la biblioteca un árbol recibido. Si ya hay un archivo con el mismo contenido, devuelve ese y no lo duplica.
    /// </summary>
    public string Importar(Arbol a, string nombreArchivo)
    {
        var texto = ArbolJson.Serializar(a);
        foreach (var ruta in Directory.EnumerateFiles(Carpeta).Where(ArbolJson.EsArchivoDeArbol))
        {
            try { if (new FileInfo(ruta).Length == System.Text.Encoding.UTF8.GetByteCount(texto) && File.ReadAllText(ruta) == texto) return ruta; }
            catch { }
        }
        var nombre = Path.GetFileNameWithoutExtension(nombreArchivo);
        if (string.IsNullOrWhiteSpace(nombre)) nombre = a.Nombre;
        var destino = RutaLibre(nombre);
        File.WriteAllText(destino, texto);
        return destino;
    }

    public void Borrar(string ruta)
    {
        File.Delete(ruta);
        _indice.Remove(ruta);
    }

    /// <summary>Cambia el nombre del archivo (y el que lleva dentro). Devuelve la ruta nueva.</summary>
    public string Renombrar(string ruta, string nombre)
    {
        var a = ArbolJson.Cargar(ruta, out _);
        a.Nombre = nombre.Trim();
        var destino = Path.GetFileNameWithoutExtension(ruta) == Limpiar(nombre) ? ruta : RutaLibre(nombre);
        ArbolJson.Guardar(destino, a);
        if (!string.Equals(destino, ruta, StringComparison.OrdinalIgnoreCase)) Borrar(ruta);
        return destino;
    }

    /// <summary>
    /// El archivo al que lleva el enlace de una tarjeta. En el escritorio es una ruta relativa («familia-perez.tree»); en
    /// el teléfono todos los árboles están en la misma carpeta, así que se busca por el nombre del archivo, con la
    /// extensión .tree o la antigua .json.
    /// </summary>
    public string? Resolver(string? enlace)
    {
        if (string.IsNullOrWhiteSpace(enlace)) return null;
        var nombre = Path.GetFileNameWithoutExtension(enlace.Replace('\\', '/').Split('/')[^1]);
        foreach (var ext in new[] { ArbolJson.Extension, ".json" })
        {
            var r = Path.Combine(Carpeta, nombre + ext);
            if (File.Exists(r)) return r;
        }
        return Directory.EnumerateFiles(Carpeta).Where(ArbolJson.EsArchivoDeArbol)
            .FirstOrDefault(r => string.Equals(Path.GetFileNameWithoutExtension(r), nombre, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Árboles de la biblioteca que comparten personas con <paramref name="a"/>, de más a menos personas en común.</summary>
    public List<(EntradaArbol Entrada, int Comunes)> Relacionados(Arbol a, string? excepto = null)
    {
        var ids = a.Personas.Select(p => p.Id).ToHashSet();
        return Listar()
            .Where(e => !string.Equals(e.Ruta, excepto, StringComparison.OrdinalIgnoreCase))
            .Select(e => (e, e.Ids.Count(ids.Contains)))
            .Where(x => x.Item2 > 0)
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    /// <summary>Un archivo temporal para compartir (WhatsApp y compañía muestran su nombre).</summary>
    public string ArchivoParaCompartir(string nombre, string extension)
    {
        var carpeta = Path.Combine(CarpetaTemporal, "compartir");
        Directory.CreateDirectory(carpeta);
        foreach (var viejo in Directory.EnumerateFiles(carpeta))
            try { if (File.GetLastWriteTimeUtc(viejo) < DateTime.UtcNow.AddDays(-1)) File.Delete(viejo); } catch { }
        return Path.Combine(carpeta, Limpiar(nombre) + extension);
    }

    public static string Limpiar(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*")) s = s.Replace(c, '_');
        s = s.Trim().TrimEnd('.');
        return s.Length == 0 ? "arbol" : s;
    }

    private string RutaLibre(string nombre)
    {
        var baseNombre = Limpiar(nombre);
        var ruta = Path.Combine(Carpeta, baseNombre + ArbolJson.Extension);
        for (int i = 2; File.Exists(ruta); i++) ruta = Path.Combine(Carpeta, $"{baseNombre} ({i}){ArbolJson.Extension}");
        return ruta;
    }
}
