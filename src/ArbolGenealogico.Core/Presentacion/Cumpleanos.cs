using ArbolGenealogico.Core.Model;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Core.Presentacion;

/// <summary>El cumpleaños de alguien de la familia (o el aniversario de su muerte) en un día, y en qué árbol está.</summary>
public sealed record Fiesta(string Ruta, string Id, string Nombre, int Anios, bool Aniversario)
{
    /// <summary>«Carmen García cumple 80 años», «Se cumplen 10 años de la muerte de Luis Pérez».</summary>
    public string Frase => Aniversario
        ? Anios == 1 ? T("Se cumple un año de la muerte de {0}", Nombre) : T("Se cumplen {1} años de la muerte de {0}", Nombre, Anios)
        : Anios == 1 ? T("{0} cumple 1 año", Nombre) : T("{0} cumple {1} años", Nombre, Anios);
}

/// <summary>
/// Los cumpleaños de un día, para avisar de ellos: solo de quien tiene puestos el día y el mes de nacimiento (sin
/// «hacia»), está vivo y no pasa de <see cref="EdadMaxima"/> años. Con <c>aniversarios</c>, también los de las muertes.
/// </summary>
public static class Cumpleanos
{
    /// <summary>Con más años y sin marcar como fallecido, seguro que ya no vive: no se le felicita.</summary>
    public const int EdadMaxima = 110;

    /// <summary>
    /// Las fiestas de ese día en esos árboles (el primero, el que se abre al tocar el aviso). Quien está en varios árboles
    /// sale una vez. Primero los cumpleaños; dentro de cada grupo, por nombre.
    /// </summary>
    public static List<Fiesta> Del(DateTime dia, IEnumerable<(string Ruta, Arbol Arbol)> arboles, bool aniversarios)
    {
        var vistos = new HashSet<string>();
        var lista = new List<Fiesta>();
        foreach (var (ruta, arbol) in arboles)
            foreach (var p in arbol.Personas)
            {
                var nombre = p.NombreCompleto;
                if (nombre.Length == 0) continue;
                if (!p.HaFallecido && Cae(p.Nacimiento, dia) is int edad and >= 1 and <= EdadMaxima)
                {
                    // la misma persona en dos árboles: el mismo id o, si se pasó a mano, el mismo nombre y fecha
                    if (vistos.Add("c" + p.Id) & vistos.Add("c" + nombre + p.Nacimiento)) lista.Add(new Fiesta(ruta, p.Id, nombre, edad, false));
                }
                else if (aniversarios && Cae(p.Defuncion, dia) is int anios and >= 1)
                {
                    if (vistos.Add("a" + p.Id) & vistos.Add("a" + nombre + p.Defuncion)) lista.Add(new Fiesta(ruta, p.Id, nombre, anios, true));
                }
            }
        return lista.OrderBy(f => f.Aniversario).ThenBy(f => f.Nombre, StringComparer.Create(Cultura, true)).ToList();
    }

    /// <summary>
    /// Los años que hace de esa fecha si ese día es su aniversario; null si no lo es o no se sabe el día exacto. Los
    /// nacidos un 29 de febrero, los años que no son bisiestos lo celebran el 28.
    /// </summary>
    public static int? Cae(Fecha? fecha, DateTime dia)
    {
        if (fecha is not { Mes: { } mes, Dia: { } d, Aproximada: false } f || f.Anio <= 0 || f.Anio > dia.Year) return null;
        bool hoy = mes == dia.Month && d == dia.Day
            || mes == 2 && d == 29 && dia.Month == 2 && dia.Day == 28 && !DateTime.IsLeapYear(dia.Year);
        return hoy ? dia.Year - f.Anio : null;
    }

    /// <summary>El título del aviso: «Hoy, en tu familia», «Mañana…», «Dentro de 7 días…».</summary>
    public static string Cuando(int dias) => dias switch
    {
        0 => T("Hoy, en tu familia"),
        1 => T("Mañana, en tu familia"),
        _ => T("Dentro de {0} días, en tu familia", dias),
    };
}
