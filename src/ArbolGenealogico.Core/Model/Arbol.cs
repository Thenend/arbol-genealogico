using System.Text.Json.Serialization;

namespace ArbolGenealogico.Core.Model;

public sealed partial class Arbol
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("raizId")] public string RaizId { get; set; } = "";
    [JsonPropertyName("personas")] public List<Persona> Personas { get; set; } = new();
    [JsonPropertyName("uniones")] public List<Union> Uniones { get; set; } = new();

    public static Arbol Nuevo(string nombre, string nombrePersona = "")
    {
        var a = new Arbol { Nombre = nombre };
        var p = a.NuevaPersona(Sexo.Desconocido);
        p.Nombre = nombrePersona;
        a.RaizId = p.Id;
        return a;
    }

    public Persona? Buscar(string id) => Personas.FirstOrDefault(p => p.Id == id);
    public Persona Obtener(string id) => Buscar(id) ?? throw new KeyNotFoundException($"Persona {id}");

    public Union? UnionComoHijo(string id) => Uniones.FirstOrDefault(u => u.Hijos.Contains(id));
    public IEnumerable<Union> UnionesComoPareja(string id) => Uniones.Where(u => u.Parejas.Contains(id));

    public Persona NuevaPersona(Sexo sexo)
    {
        var p = new Persona { Id = SiguienteId("p", Personas.Select(x => x.Id)), Sexo = sexo };
        Personas.Add(p);
        return p;
    }

    public Union NuevaUnion(params string[] parejas)
    {
        var u = new Union { Id = SiguienteId("u", Uniones.Select(x => x.Id)), Parejas = parejas.ToList() };
        Uniones.Add(u);
        return u;
    }

    private static string SiguienteId(string prefijo, IEnumerable<string> existentes)
    {
        var set = new HashSet<string>(existentes);
        int n = set.Count + 1;
        while (set.Contains(prefijo + n)) n++;
        return prefijo + n;
    }

    public Arbol Clonar() => new()
    {
        Version = Version, Nombre = Nombre, RaizId = RaizId,
        Personas = Personas.Select(p => p.Clonar()).ToList(),
        Uniones = Uniones.Select(u => u.Clonar()).ToList(),
    };
}
