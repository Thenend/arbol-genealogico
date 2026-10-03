namespace ArbolGenealogico.Core.Model;

/// <summary>Operaciones de edición. Mantienen los invariantes del árbol.</summary>
public sealed partial class Arbol
{
    private static Sexo Opuesto(Sexo s) => s switch { Sexo.Hombre => Sexo.Mujer, Sexo.Mujer => Sexo.Hombre, _ => Sexo.Desconocido };

    /// <summary>Añade pareja. Si ya hay una unión con solo esta persona (otro progenitor desconocido) la completa.</summary>
    public Persona AnadirPareja(string id)
    {
        var p = Obtener(id);
        var q = NuevaPersona(Opuesto(p.Sexo));
        var libre = UnionesComoPareja(id).FirstOrDefault(u => u.Parejas.Count == 1);
        if (libre != null) libre.Parejas.Add(q.Id);
        else NuevaUnion(id, q.Id);
        return q;
    }

    /// <summary>Añade un hijo. <paramref name="unionId"/> puede ser null si la persona tiene 0 o 1 uniones.</summary>
    public Persona AnadirHijo(string id, string? unionId = null)
    {
        Obtener(id);
        Union u;
        if (unionId != null) u = Uniones.First(x => x.Id == unionId && x.Parejas.Contains(id));
        else
        {
            var us = UnionesComoPareja(id).ToList();
            if (us.Count > 1) throw new InvalidOperationException("Hay varias uniones: indica cuál.");
            u = us.Count == 1 ? us[0] : NuevaUnion(id);
        }
        var h = NuevaPersona(Sexo.Desconocido);
        u.Hijos.Add(h.Id);
        return h;
    }

    /// <summary>Crea los padres (o completa el progenitor que falta). Devuelve las personas nuevas.</summary>
    public List<Persona> AnadirPadres(string id)
    {
        if (!EsSanguinea(id)) throw new InvalidOperationException("La familia de una pareja política vive en su propio árbol.");
        var u = UnionComoHijo(id);
        var nuevos = new List<Persona>();
        if (u == null)
        {
            var padre = NuevaPersona(Sexo.Hombre);
            var madre = NuevaPersona(Sexo.Mujer);
            u = NuevaUnion(padre.Id, madre.Id);
            u.Hijos.Add(id);
            nuevos.Add(padre); nuevos.Add(madre);
        }
        else if (u.Parejas.Count == 1)
        {
            var existente = Obtener(u.Parejas[0]);
            var p = NuevaPersona(Opuesto(existente.Sexo));
            u.Parejas.Add(p.Id);
            nuevos.Add(p);
        }
        else throw new InvalidOperationException("Ya tiene los dos padres.");
        return nuevos;
    }

    /// <summary>Añade un hermano justo después de la persona. Si no tiene padres, los crea sin nombre.</summary>
    public (Persona hermano, List<Persona> padresNuevos) AnadirHermano(string id)
    {
        if (!EsSanguinea(id)) throw new InvalidOperationException("La familia de una pareja política vive en su propio árbol.");
        var padresNuevos = new List<Persona>();
        var u = UnionComoHijo(id);
        if (u == null) { padresNuevos = AnadirPadres(id); u = UnionComoHijo(id)!; }
        var h = NuevaPersona(Sexo.Desconocido);
        u.Hijos.Insert(u.Hijos.IndexOf(id) + 1, h.Id);
        return (h, padresNuevos);
    }

    /// <summary>Elimina a la persona y lo que quede desconectado de la principal. Devuelve todas las eliminadas.</summary>
    public List<Persona> Eliminar(string id)
    {
        if (id == RaizId) throw new InvalidOperationException("No se puede eliminar a la persona principal.");
        var eliminadas = new List<Persona>();
        QuitarPersona(Obtener(id), eliminadas);
        LimpiarUniones();

        var alcanzables = Alcanzables();
        foreach (var x in Personas.Where(x => !alcanzables.Contains(x.Id)).ToList())
            QuitarPersona(x, eliminadas);
        LimpiarUniones();
        return eliminadas;
    }

    private void LimpiarUniones()
    {
        foreach (var u in Uniones.ToList())
            if (u.Parejas.Count == 0 || (u.Parejas.Count == 1 && u.Hijos.Count == 0))
                Uniones.Remove(u);
    }

    private void QuitarPersona(Persona p, List<Persona> eliminadas)
    {
        Personas.Remove(p);
        eliminadas.Add(p);
        foreach (var u in Uniones) { u.Parejas.Remove(p.Id); u.Hijos.Remove(p.Id); }
    }

    public List<Persona> PersonasQueSeEliminarian(string id) => Clonar().Eliminar(id);

    /// <summary>Repara referencias rotas y duplicados. Devuelve avisos.</summary>
    public List<string> Reparar()
    {
        var avisos = new List<string>();
        Personas = Personas.Where(p => p != null).ToList();
        var vistos = new HashSet<string>();
        foreach (var p in Personas.ToList())
            if (string.IsNullOrEmpty(p.Id) || !vistos.Add(p.Id)) { Personas.Remove(p); avisos.Add("Persona duplicada o sin id descartada."); }

        var ids = Personas.Select(p => p.Id).ToHashSet();
        var idsUnion = new HashSet<string>();
        var hijoDe = new HashSet<string>();
        foreach (var u in Uniones.ToList())
        {
            if (string.IsNullOrEmpty(u.Id) || !idsUnion.Add(u.Id))
            {
                u.Id = SiguienteId("u", idsUnion.Concat(Uniones.Select(x => x.Id)));
                idsUnion.Add(u.Id);
            }
            u.Parejas = u.Parejas.Where(ids.Contains).Distinct().ToList();
            if (u.Parejas.Count > 2) { u.Parejas = u.Parejas.Take(2).ToList(); avisos.Add($"La unión {u.Id} tenía más de dos parejas."); }
            u.Hijos = u.Hijos.Where(h => ids.Contains(h) && !u.Parejas.Contains(h) && hijoDe.Add(h)).ToList();
            if (u.Parejas.Count == 0) Uniones.Remove(u);
        }
        if (Buscar(RaizId) == null) RaizId = Personas.FirstOrDefault()?.Id ?? "";
        var alc = Alcanzables();
        int sueltas = Personas.Count(p => !alc.Contains(p.Id));
        if (sueltas > 0) avisos.Add($"{sueltas} persona(s) no están conectadas con la principal y no se mostrarán.");
        return avisos;
    }
}
