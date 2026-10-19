using System.Text.Json;

namespace ArbolGenealogico.Movil;

/// <summary>Lo que se recuerda de una vez para otra (la forma de las tarjetas y la colocación del árbol).</summary>
public sealed class Preferencias
{
    public string Forma { get; set; } = "horizontal foto";
    public string Ordenacion { get; set; } = "A";
    /// <summary>Marcar en las tarjetas a quien tiene algo escrito en su historia.</summary>
    public bool MarcaHistoria { get; set; } = true;
    /// <summary>Al imprimir: el papel («A4», «Letter»…; null hasta elegirlo: el del país), las hojas y los colores.</summary>
    public string? PapelImpresion { get; set; }
    public int HojasImpresion { get; set; } = 1;
    public string EstiloImpresion { get; set; } = "Claro";
    /// <summary>Al imprimir en varias hojas, cortar a partes iguales aunque se parta alguna tarjeta.</summary>
    public bool CortesIguales { get; set; }
    /// <summary>Copia automática de los árboles en Documentos (para recuperarlos si se borra la aplicación).</summary>
    public bool CopiaEnDocumentos { get; set; } = true;
    /// <summary>
    /// Si se compró la versión completa, según la última vez que contestó la tienda (para no depender de la conexión: se
    /// vuelve a preguntar al abrir la aplicación).
    /// </summary>
    public bool Completa { get; set; }

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
