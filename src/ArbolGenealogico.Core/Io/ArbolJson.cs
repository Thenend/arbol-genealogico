using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Io;

public static class ArbolJson
{
    private sealed class SexoConverter : JsonConverter<Sexo>
    {
        public override Sexo Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) =>
            (r.GetString() ?? "").Trim().ToUpperInvariant() switch
            {
                "M" or "H" or "HOMBRE" or "MALE" => Sexo.Hombre,
                "F" or "MUJER" or "FEMALE" => Sexo.Mujer,
                _ => Sexo.Desconocido,
            };

        public override void Write(Utf8JsonWriter w, Sexo v, JsonSerializerOptions o) =>
            w.WriteStringValue(v switch { Sexo.Hombre => "M", Sexo.Mujer => "F", _ => "U" });
    }

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new SexoConverter() },
    };

    /// <summary>Extensión de los árboles. Los antiguos (.json) se siguen abriendo.</summary>
    public const string Extension = ".tree";

    public static bool EsArchivoDeArbol(string ruta) =>
        ruta.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || ruta.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public static string Serializar(Arbol a) => JsonSerializer.Serialize(a, Opciones);

    public static Arbol Deserializar(string json, out List<string> avisos)
    {
        var a = JsonSerializer.Deserialize<Arbol>(json, Opciones) ?? throw new InvalidDataException("JSON vacío.");
        if (a.Personas == null || a.Uniones == null) throw new InvalidDataException("No es un árbol genealógico.");
        avisos = a.Reparar();
        if (a.Version > Arbol.VersionActual)
            avisos.Add("Este árbol se creó con una versión más nueva del programa: puede que algo no se vea.");
        // La huella es el contenido ya normalizado: no depende de espacios ni del orden de las propiedades.
        a.MigrarIds(Serializar(a));
        return a;
    }

    public static Arbol Cargar(string ruta, out List<string> avisos) =>
        Deserializar(File.ReadAllText(ruta), out avisos);

    public static void Guardar(string ruta, Arbol a)
    {
        var tmp = ruta + ".tmp";
        File.WriteAllText(tmp, Serializar(a));
        File.Move(tmp, ruta, overwrite: true);
    }

    /// <summary>Resuelve la ruta de un árbol enlazado respecto al JSON que lo contiene.</summary>
    public static string ResolverRuta(string jsonOrigen, string enlace) =>
        Path.GetFullPath(Path.IsPathRooted(enlace)
            ? enlace
            : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(jsonOrigen))!, enlace));

    public static string RutaRelativa(string jsonOrigen, string destino)
    {
        try { return Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(jsonOrigen))!, destino); }
        catch { return destino; }
    }
}
