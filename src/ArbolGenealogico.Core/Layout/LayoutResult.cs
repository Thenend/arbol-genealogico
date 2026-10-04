namespace ArbolGenealogico.Core.Layout;

public readonly record struct Pt(double X, double Y);

/// <summary>
/// Cómo se coloca el árbol (en la interfaz: A = «Compacto», B = «Lateral», C = «Balanceado», D = «Escalonado», E = «Bowtie»).
/// A: generaciones en filas con parejas que bajan cuando hace falta. B: estilo Family TreePhoto.
/// C: como B, con los hermanos de cada antepasado repartidos a los dos lados de su línea y la rama más grande por fuera.
/// D: como C, con las familias escalonadas (las de fuera más arriba) para que el árbol sea lo más estrecho posible.
/// E: árbol de antepasados en horizontal, en pajarita: los del padre hacia la izquierda y los de la madre hacia la derecha.
/// </summary>
public enum Ordenacion { A, B, C, D, E }

public sealed class LayoutOptions
{
    public Ordenacion Ordenacion { get; set; } = Ordenacion.A;
    public double AnchoCarta { get; set; } = 230;
    public double AltoCarta { get; set; } = 88;
    /// <summary>Hueco entre las dos tarjetas de una pareja.</summary>
    public double HuecoPareja { get; set; } = 40;
    /// <summary>Hueco entre hermanos (o primos de la misma rama).</summary>
    public double HuecoHermanos { get; set; } = 26;
    /// <summary>Hueco entre familias distintas de una misma fila.</summary>
    public double HuecoFamilias { get; set; } = 64;
    /// <summary>Espacio vertical entre filas (por donde discurren los buses).</summary>
    public double HuecoFilas { get; set; } = 100;
    public double Margen { get; set; } = 60;
    /// <summary>Peso de las aristas de la línea directa (las endereza).</summary>
    public double PesoLineaDirecta { get; set; } = 8;
    /// <summary>
    /// Cuánto más pesa colocar a los padres sobre sus hijos que colocar a los hijos bajo sus padres.
    /// Un valor alto hace el árbol más ancho pero cada pareja queda centrada sobre su descendencia.
    /// </summary>
    public double FactorPadreSobreHijos { get; set; } = 100;
    public int MaxBarridos { get; set; } = 6000;
}

public sealed class CartaPos
{
    public string Id { get; init; } = "";
    /// <summary>Centro horizontal.</summary>
    public double X { get; set; }
    /// <summary>Borde superior.</summary>
    public double Y { get; set; }
    public int Generacion { get; init; }
    public int Cluster { get; init; }
    public int IndiceEnCluster { get; init; }
    public bool Directa { get; init; }
    public bool Politica { get; init; }
}

public enum TipoConexion { Pareja, Descendencia }

public sealed class Conexion
{
    public string UnionId { get; init; } = "";
    public TipoConexion Tipo { get; init; }
    public bool Directa { get; init; }
    public string? HijoId { get; init; }
    public List<Pt> Puntos { get; init; } = new();
    /// <summary>En un enlace de pareja, el punto del que cuelgan los hijos si no es el centro de la línea.</summary>
    public Pt? Nudo { get; set; }
}

public sealed class LayoutResult
{
    public LayoutOptions Opciones { get; init; } = new();
    public Dictionary<string, CartaPos> Cartas { get; init; } = new();
    public List<Conexion> Conexiones { get; init; } = new();
    /// <summary>Filas (de arriba abajo) → clusters (de izquierda a derecha) → ids de personas.</summary>
    public List<List<List<string>>> Filas { get; init; } = new();
    public double Ancho { get; set; }
    public double Alto { get; set; }
}
