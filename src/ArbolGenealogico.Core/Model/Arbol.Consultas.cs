using System.Globalization;
using System.Text;

namespace ArbolGenealogico.Core.Model;

public static class Texto
{
    /// <summary>Minúsculas y sin tildes ni diacríticos ("Ñandú" → "nandu"), para comparar sin fijarse en ellos.</summary>
    public static string Normalizar(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}

public sealed partial class Arbol
{
    /// <summary>
    /// Personas cuyo nombre completo (nombre y apellidos) contiene todas las palabras de la consulta,
    /// sin distinguir mayúsculas ni tildes. Una consulta vacía no encuentra a nadie.
    /// </summary>
    public List<Persona> BuscarPorNombre(string? consulta)
    {
        var terminos = Texto.Normalizar(consulta).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terminos.Length == 0) return new();
        return Personas.Where(p =>
        {
            var nombre = Texto.Normalizar(p.NombreCompleto);
            return terminos.All(t => nombre.Contains(t, StringComparison.Ordinal));
        }).ToList();
    }

    /// <summary>
    /// Línea sanguínea respecto a la principal: ella, sus antepasados y todos los descendientes de esos antepasados
    /// (hermanos, tíos, primos...). Las parejas de esas personas (la mujer del tío) son "políticas" aunque
    /// sean padres o madres de primos.
    /// </summary>
    public HashSet<string> Sanguineos() => SanguineosDe(RaizId);

    public HashSet<string> SanguineosDe(string raiz)
    {
        var res = new HashSet<string>();
        if (Buscar(raiz) == null) return res;
        var ancestros = new HashSet<string> { raiz };
        var cola = new Queue<string>();
        cola.Enqueue(raiz);
        while (cola.Count > 0)
        {
            var up = UnionComoHijo(cola.Dequeue());
            if (up == null) continue;
            foreach (var q in up.Parejas) if (ancestros.Add(q)) cola.Enqueue(q);
        }
        foreach (var a in ancestros) { res.Add(a); cola.Enqueue(a); }
        while (cola.Count > 0)
            foreach (var u in UnionesComoPareja(cola.Dequeue()))
                foreach (var h in u.Hijos) if (res.Add(h)) cola.Enqueue(h);
        return res;
    }

    public bool EsSanguinea(string id) => Sanguineos().Contains(id);

    /// <summary>
    /// Una persona puede ser la principal si todas las que muestran padres en este árbol son sanguíneas respecto a ella
    /// (si no, la familia política quedaría mezclada con la propia).
    /// </summary>
    public bool PuedeSerPrincipal(string id)
    {
        var sang = SanguineosDe(id);
        return Personas.All(p => UnionComoHijo(p.Id) == null || sang.Contains(p.Id));
    }

    /// <summary>Persona principal y todos sus antepasados.</summary>
    public HashSet<string> LineaDirecta()
    {
        var res = new HashSet<string>();
        if (Buscar(RaizId) == null) return res;
        var cola = new Queue<string>();
        res.Add(RaizId); cola.Enqueue(RaizId);
        while (cola.Count > 0)
        {
            var up = UnionComoHijo(cola.Dequeue());
            if (up == null) continue;
            foreach (var q in up.Parejas) if (res.Add(q)) cola.Enqueue(q);
        }
        return res;
    }

    /// <summary>Todo lo conectado con la principal (parejas, padres, hijos).</summary>
    public HashSet<string> Alcanzables()
    {
        var res = new HashSet<string>();
        if (Buscar(RaizId) == null) return res;
        var cola = new Queue<string>();
        res.Add(RaizId); cola.Enqueue(RaizId);
        while (cola.Count > 0)
        {
            var id = cola.Dequeue();
            foreach (var u in Uniones.Where(u => u.Parejas.Contains(id) || u.Hijos.Contains(id)))
            {
                foreach (var q in u.Parejas) if (res.Add(q)) cola.Enqueue(q);
                foreach (var q in u.Hijos) if (res.Add(q)) cola.Enqueue(q);
            }
        }
        return res;
    }

    /// <summary>Antepasados (padres, abuelos...) y descendientes (hijos, nietos...) de una persona, sin incluirla.</summary>
    public (HashSet<string> Antepasados, HashSet<string> Descendientes) Linaje(string id)
    {
        var antepasados = new HashSet<string>();
        var cola = new Queue<string>();
        cola.Enqueue(id);
        while (cola.Count > 0)
        {
            var up = UnionComoHijo(cola.Dequeue());
            if (up == null) continue;
            foreach (var q in up.Parejas) if (q != id && antepasados.Add(q)) cola.Enqueue(q);
        }
        var descendientes = new HashSet<string>();
        cola.Enqueue(id);
        while (cola.Count > 0)
            foreach (var u in UnionesComoPareja(cola.Dequeue()))
                foreach (var h in u.Hijos) if (h != id && descendientes.Add(h)) cola.Enqueue(h);
        return (antepasados, descendientes);
    }

    /// <summary>
    /// Familia directa de una persona: ella, sus antepasados, sus descendientes, sus parejas y sus hermanos
    /// (también los medio hermanos, hijos de otra unión de sus padres).
    /// </summary>
    public HashSet<string> FamiliaDirecta(string id)
    {
        var (antepasados, descendientes) = Linaje(id);
        var familia = new HashSet<string>(antepasados);
        familia.UnionWith(descendientes);
        familia.Add(id);
        foreach (var u in UnionesComoPareja(id)) foreach (var q in u.Parejas) familia.Add(q);
        var origen = UnionComoHijo(id);
        if (origen != null)
            foreach (var padre in origen.Parejas)
                foreach (var u in UnionesComoPareja(padre))
                    foreach (var h in u.Hijos) familia.Add(h);
        return familia;
    }

    public bool TienePadresCompletos(string id)
    {
        var u = UnionComoHijo(id);
        return u != null && u.Parejas.Count >= 2;
    }
}
