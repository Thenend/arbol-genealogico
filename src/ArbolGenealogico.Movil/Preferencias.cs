using System.Text.Json;

namespace ArbolGenealogico.Movil;

/// <summary>Lo que se recuerda de una vez para otra (la forma de las tarjetas y la colocación del árbol).</summary>
public sealed class Preferencias
{
    public string Forma { get; set; } = "horizontal foto";
    public string Ordenacion { get; set; } = "C";
    /// <summary>Copia automática de los árboles en Documentos (para recuperarlos si se borra la aplicación).</summary>
    public bool CopiaEnDocumentos { get; set; } = true;

    private static string Ruta => Path.Combine(App.Plataforma.CarpetaDatos, "preferencias.json");
    private static Preferencias? _actual;

    public static Preferencias Actual
    {
        get
        {
            if (_actual != null) return _actual;
            try { _actual = File.Exists(Ruta) ? JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) : null; }
            catch { _actual = null; }
            return _actual ??= new Preferencias();
        }
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            File.WriteAllText(Ruta, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
