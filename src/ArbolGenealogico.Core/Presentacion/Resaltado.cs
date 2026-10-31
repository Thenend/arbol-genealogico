using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Core.Presentacion;

/// <summary>
/// Lo que se resalta al seleccionar a alguien: las líneas de su linaje (hacia sus antepasados y descendientes), las que
/// llevan a otros familiares cercanos (hermanos, cónyuges; se ven con normalidad) y su familia directa (el resto se atenúa).
/// </summary>
public sealed record Resaltado(HashSet<Conexion> Linaje, HashSet<Conexion> Cercanas, HashSet<string>? Familia)
{
    public static readonly Resaltado Ninguno = new(new(), new(), null);

    public static Resaltado De(Arbol arbol, LayoutResult layout, string? sel)
    {
        if (sel == null || arbol.Buscar(sel) == null) return Ninguno;
        var res = new HashSet<Conexion>();
        var cercanas = new HashSet<Conexion>();
        var (antepasados, descendientes) = arbol.Linaje(sel);
        var familia = arbol.FamiliaDirecta(sel);
        antepasados.Add(sel); descendientes.Add(sel);
        var uniones = arbol.Uniones.ToDictionary(u => u.Id);
        var conLinaje = new HashSet<string>();
        foreach (var c in layout.Conexiones.Where(c => c.Tipo == TipoConexion.Descendencia))
        {
            if (!uniones.TryGetValue(c.UnionId, out var u)) continue;
            bool sube = c.HijoId != null && antepasados.Contains(c.HijoId);   // de la persona (o un antepasado) hacia sus padres
            bool baja = u.Parejas.Any(descendientes.Contains);                // de la persona (o un descendiente) hacia sus hijos
            if (sube || baja) { res.Add(c); conLinaje.Add(c.UnionId); }
        }
        foreach (var c in layout.Conexiones.Where(c => c.Tipo == TipoConexion.Pareja && conLinaje.Contains(c.UnionId))) res.Add(c);
        foreach (var c in layout.Conexiones)
        {
            if (res.Contains(c) || !uniones.TryGetValue(c.UnionId, out var u) || !u.Parejas.Any(familia.Contains)) continue;
            bool cerca = c.Tipo == TipoConexion.Pareja
                ? u.Parejas.All(familia.Contains) || u.Hijos.Any(familia.Contains)   // su cónyuge, o los padres de un medio hermano
                : c.HijoId != null && familia.Contains(c.HijoId);                    // hacia un hermano o medio hermano
            if (cerca) cercanas.Add(c);
        }
        return new(res, cercanas, familia);
    }
}

public static class Nombres
{
    public static string Ordenacion(Ordenacion o) => o switch
    {
        Layout.Ordenacion.A => T("Compacto"),
        Layout.Ordenacion.B => T("Lateral"),
        Layout.Ordenacion.C => T("Balanceado"),
        Layout.Ordenacion.D => T("Escalonado"),
        Layout.Ordenacion.E => T("Horizontal"),
        _ => T("Horizontal escalonado"),
    };

    public static string Iniciales(Persona p)
    {
        static string Ini(string s) => string.IsNullOrWhiteSpace(s) ? "" : char.ToUpperInvariant(s.Trim()[0]).ToString();
        var r = Ini(p.Nombre) + Ini(p.Apellidos);
        return r.Length > 0 ? r : "?";
    }

    public static bool SinNombre(Persona p) => string.IsNullOrWhiteSpace(p.Nombre) && string.IsNullOrWhiteSpace(p.Apellidos);

    public static string Mostrar(Persona? p) => p == null ? T("pareja desconocida") : SinNombre(p) ? T("sin nombre") : p.NombreCompleto;
}
