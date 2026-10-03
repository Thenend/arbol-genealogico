using System.Text.Json.Serialization;

namespace ArbolGenealogico.Core.Model;

public enum Sexo { Desconocido, Hombre, Mujer }

public sealed class Persona
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("apellidos")] public string Apellidos { get; set; } = "";
    [JsonPropertyName("sexo")] public Sexo Sexo { get; set; } = Sexo.Desconocido;

    /// <summary>Foto en JPEG/PNG codificada en base64 (opcional).</summary>
    [JsonPropertyName("foto"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Foto { get; set; }

    [JsonPropertyName("historia")] public string Historia { get; set; } = "";

    /// <summary>Ruta (relativa al JSON) del árbol propio de esta persona (opcional).</summary>
    [JsonPropertyName("arbolEnlazado"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArbolEnlazado { get; set; }

    [JsonIgnore]
    public string NombreCompleto => $"{Nombre} {Apellidos}".Trim();

    public Persona Clonar() => (Persona)MemberwiseClone();
}

/// <summary>Pareja (1 o 2 personas; la otra puede ser desconocida) y sus hijos, en orden.</summary>
public sealed class Union
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("parejas")] public List<string> Parejas { get; set; } = new();
    [JsonPropertyName("hijos")] public List<string> Hijos { get; set; } = new();

    public Union Clonar() => new() { Id = Id, Parejas = new(Parejas), Hijos = new(Hijos) };
}
