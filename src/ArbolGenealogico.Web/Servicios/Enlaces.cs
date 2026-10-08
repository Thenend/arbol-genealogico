using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Web.Servicios;

/// <summary>
/// Árboles enlazados. En la web, el enlace de una tarjeta («arbolEnlazado») es el identificador del otro árbol; en los
/// archivos de escritorio es la ruta de su .json. Al subir varios .json a la vez se conectan por el nombre del archivo, y al
/// descargarlos se vuelven a escribir como «nombre del árbol.json» para que la aplicación de escritorio los encuentre.
/// </summary>
public static class Enlaces
{
    public static bool EsWeb(string? enlace) => Guid.TryParse(enlace, out _);

    public static string NombreArchivo(string? ruta)
    {
        var s = (ruta ?? "").Replace('\\', '/');
        return s[(s.LastIndexOf('/') + 1)..];
    }

    public static string ArchivoPara(string nombreArbol)
    {
        var invalidos = "<>:\"/\\|?*".ToCharArray();
        var limpio = new string(nombreArbol.Select(c => invalidos.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return (limpio.Length == 0 ? "arbol" : limpio) + ".json";
    }

    public sealed record Resultado(int Arboles, int Conectados, List<string> SinEncontrar, List<string> Errores);

    /// <summary>Sube varios .json de la aplicación de escritorio y conecta entre sí los enlaces de sus tarjetas.</summary>
    public static async Task<Resultado> Importar(Datos datos, IReadOnlyList<(string Nombre, string Texto)> archivos, IEnumerable<ResumenArbol> existentes)
    {
        var errores = new List<string>();
        var creados = new List<(string Id, Arbol Arbol)>();
        var porArchivo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nombre, texto) in archivos)
        {
            try
            {
                var a = ArbolJson.Deserializar(texto, out _);
                if (string.IsNullOrWhiteSpace(a.Nombre)) a.Nombre = Path.GetFileNameWithoutExtension(nombre);
                var id = await datos.Crear(a.Nombre, ArbolJson.Serializar(a));
                creados.Add((id, a));
                porArchivo[nombre] = id;
            }
            catch (Exception e) { errores.Add($"{nombre}: {e.Message}"); }
        }
        // también se puede enlazar con árboles que ya estaban en la web, por su nombre de archivo («Familia Pérez.json»)
        foreach (var r in existentes) porArchivo.TryAdd(ArchivoPara(r.Nombre), r.Id);

        int conectados = 0;
        var sinEncontrar = new List<string>();
        foreach (var (id, a) in creados)
        {
            bool cambiado = false;
            foreach (var p in a.Personas.Where(p => !string.IsNullOrEmpty(p.ArbolEnlazado) && !EsWeb(p.ArbolEnlazado)))
            {
                if (porArchivo.TryGetValue(NombreArchivo(p.ArbolEnlazado), out var destino)) { p.ArbolEnlazado = destino; conectados++; cambiado = true; }
                else sinEncontrar.Add($"{p.NombreCompleto} → {NombreArchivo(p.ArbolEnlazado)}");
            }
            if (cambiado)
            {
                try { await datos.Guardar(id, a.Nombre, ArbolJson.Serializar(a), null); }
                catch (Exception e) { errores.Add($"{a.Nombre}: {e.Message}"); }
            }
        }
        return new Resultado(creados.Count, conectados, sinEncontrar, errores);
    }

    /// <summary>El JSON para la aplicación de escritorio: los enlaces a otros árboles de la web pasan a «nombre del árbol.json».</summary>
    public static string ParaEscritorio(Arbol arbol, IEnumerable<ResumenArbol> conocidos)
    {
        var nombres = conocidos.ToDictionary(r => r.Id, r => r.Nombre);
        var copia = arbol.Clonar();
        foreach (var p in copia.Personas.Where(p => EsWeb(p.ArbolEnlazado)))
            p.ArbolEnlazado = nombres.TryGetValue(p.ArbolEnlazado!, out var n) ? ArchivoPara(n) : null;
        return ArbolJson.Serializar(copia);
    }
}
