using System.Text;
using System.Text.Json;

namespace ArbolGenealogico.Core.Io;

/// <summary>
/// Todos los árboles en un solo archivo, para guardarlo fuera del teléfono (en Drive, en el correo, en el de un familiar) y
/// recuperarlos de una vez en otro. Es un .ramo como los demás (así se abre con Ramo desde cualquier sitio) que lleva
/// dentro cada árbol tal cual, con el nombre de su archivo:
/// <c>{"copiaCompletaRamo": 1, "fecha": "…", "arboles": [{"archivo": "familia-garcia.ramo", "arbol": {…}}]}</c>.
/// </summary>
public static class CopiaCompleta
{
    /// <summary>La marca que la distingue de un árbol; va la primera, para reconocerla sin leer el archivo entero.</summary>
    private const string Marca = "copiaCompletaRamo";
    public const int Version = 1;

    public sealed record Leida(DateTime? Fecha, IReadOnlyList<(string Archivo, string Contenido)> Arboles);

    /// <summary>Si esos datos son una copia completa (y no un árbol).</summary>
    public static bool Es(byte[] datos)
    {
        var principio = Encoding.UTF8.GetString(datos, 0, Math.Min(datos.Length, 200));
        return principio.Contains('"' + Marca + '"');
    }

    /// <summary>
    /// La copia de esos árboles (el nombre de su archivo y su contenido, el JSON de un árbol). Los que no son JSON válido
    /// (un archivo dañado) no entran.
    /// </summary>
    public static byte[] Crear(IEnumerable<(string Archivo, string Contenido)> arboles, DateTime fecha)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteNumber(Marca, Version);
            w.WriteString("fecha", fecha.ToString("s"));
            // las versiones de Ramo de antes de la copia completa, al abrirla, dicen que no es un árbol (sin esto la tomarían
            // por un árbol vacío)
            w.WriteNull("personas");
            w.WriteNull("uniones");
            w.WriteStartArray("arboles");
            foreach (var (archivo, contenido) in arboles)
            {
                try { using var _ = JsonDocument.Parse(contenido, Lectura); }
                catch (JsonException) { continue; }
                w.WriteStartObject();
                w.WriteString("archivo", archivo);
                w.WritePropertyName("arbol");
                w.WriteRawValue(contenido, skipInputValidation: true);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    /// <summary>Los árboles que lleva la copia, cada uno como el JSON de un árbol (para <see cref="ArbolJson.Deserializar"/>).</summary>
    public static Leida Leer(byte[] datos)
    {
        using var doc = JsonDocument.Parse(datos, Lectura);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty(Marca, out _))
            throw new InvalidDataException("No es una copia completa de Ramo.");
        DateTime? fecha = raiz.TryGetProperty("fecha", out var f) && f.ValueKind == JsonValueKind.String
            && DateTime.TryParse(f.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
        var arboles = new List<(string, string)>();
        if (raiz.TryGetProperty("arboles", out var lista) && lista.ValueKind == JsonValueKind.Array)
            foreach (var e in lista.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("arbol", out var a) || a.ValueKind != JsonValueKind.Object) continue;
                var archivo = e.TryGetProperty("archivo", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
                arboles.Add((archivo, a.GetRawText()));
            }
        return new Leida(fecha, arboles);
    }

    private static readonly JsonDocumentOptions Lectura = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
}
