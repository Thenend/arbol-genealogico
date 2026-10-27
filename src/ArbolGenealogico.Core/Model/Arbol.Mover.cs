using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Core.Model;

/// <summary>Lo que se quiere que sea una persona de otra al cambiar su parentesco.</summary>
public enum Parentesco { PadreOMadre, Hermano, Pareja, Hijo }

/// <summary>
/// Un sitio de la persona en el árbol: hija de una unión (sus padres) o pareja en una unión con otra persona. Es lo que
/// se quita al cambiar su parentesco.
/// </summary>
public sealed record Vinculo(string UnionId, bool ComoHijo);

/// <summary>Lo que ha hecho (o haría) un cambio de parentesco.</summary>
public sealed record ResultadoMover(List<Persona> Quitadas, int Acompanan);

/// <summary>
/// Cambiar el parentesco de una persona mal colocada (el hermano metido como hermano del padre, una fusión que la dejó
/// donde no era): se quita uno de sus vínculos y se le pone el nuevo. Lo que cuelga de ella (sus parejas, sus hijos…)
/// va con ella. Lo que no tenga sentido (ciclos, una pareja política con padres, la persona principal) no se hace.
/// </summary>
public sealed partial class Arbol
{
    /// <summary>
    /// Los vínculos que se le pueden cambiar a una persona: sus padres y las uniones con otra persona. Una unión en la
    /// que está sola con sus hijos no es un vínculo con nadie: sus hijos van con ella.
    /// </summary>
    public List<Vinculo> Vinculos(string id)
    {
        var res = new List<Vinculo>();
        if (UnionComoHijo(id) is { } padres) res.Add(new Vinculo(padres.Id, true));
        foreach (var u in UnionesComoPareja(id))
            if (u.Parejas.Count == 2) res.Add(new Vinculo(u.Id, false));
        return res;
    }

    /// <summary>Por qué no se le puede cambiar el parentesco a nadie así (o null si se puede intentar).</summary>
    public string? PorQueNoSeMueve(string id)
    {
        if (id == RaizId) return T("Es la persona principal: el árbol se ve desde ella.");
        if (Vinculos(id).Count == 0) return T("No tiene ningún parentesco que cambiar.");
        return null;
    }

    /// <summary>
    /// Cambia el parentesco de <paramref name="id"/>: le quita <paramref name="quitar"/> y la hace
    /// <paramref name="nuevo"/> de <paramref name="otroId"/> (hija de su unión <paramref name="unionId"/> si tiene
    /// varias). Si se quita una unión con hijos, <paramref name="hijosConElla"/> dice si se van con ella o se quedan con
    /// la otra persona. Lo que se quede sin unir a la principal se quita. Si no se puede, lanza
    /// <see cref="InvalidOperationException"/> con el motivo y no cambia nada.
    /// </summary>
    public ResultadoMover Mover(string id, Vinculo quitar, Parentesco nuevo, string otroId, string? unionId = null, bool hijosConElla = false)
    {
        var c = Clonar();
        var r = c.MoverEn(id, quitar, nuevo, otroId, unionId, hijosConElla);
        Personas = c.Personas;
        Uniones = c.Uniones;
        return r;
    }

    /// <summary>Lo que haría <see cref="Mover"/>, sin cambiar nada: el resultado, o el motivo por el que no se puede.</summary>
    public (ResultadoMover? Resultado, string? Error) ProbarMover(string id, Vinculo quitar, Parentesco nuevo, string otroId, string? unionId = null, bool hijosConElla = false)
    {
        try { return (Clonar().MoverEn(id, quitar, nuevo, otroId, unionId, hijosConElla), null); }
        catch (InvalidOperationException ex) { return (null, ex.Message); }
    }

    private ResultadoMover MoverEn(string id, Vinculo quitar, Parentesco nuevo, string otroId, string? unionId, bool hijosConElla)
    {
        static InvalidOperationException No(string motivo) => new(motivo);
        if (PorQueNoSeMueve(id) is { } motivo) throw No(motivo);
        Obtener(id);
        Obtener(otroId);
        if (otroId == id) throw No(T("Elige a otra persona."));
        if (!Vinculos(id).Contains(quitar)) throw No(T("Ese parentesco ya no está en el árbol."));
        var antes = Firma();
        bool encajaba = GeneracionesCoherentes();

        // 1. Se quita el vínculo
        var u = Uniones.First(x => x.Id == quitar.UnionId);
        Union? suya = null;   // la unión con sus hijos que se lleva (si se los lleva)
        if (quitar.ComoHijo) u.Hijos.Remove(id);
        else
        {
            var otro = u.Parejas.First(x => x != id);
            if (hijosConElla && u.Hijos.Count > 0) { u.Parejas.Remove(otro); suya = u; }
            else u.Parejas.Remove(id);
            u.QuitarBoda();     // ya no son pareja
        }
        if (quitar.ComoHijo) QuitarPadresVacios(u);

        // 2. Lo que se va con ella: lo que queda unido a ella pero ya no a la principal
        var conElla = AlcanzablesDesde(id);
        int acompanan = conElla.Contains(RaizId) ? 0 : conElla.Count - 1;
        if (conElla.Contains(otroId) && !conElla.Contains(RaizId))
            throw No(T("No puede ser familiar de alguien de su propia rama."));

        // 3. Se pone el nuevo
        switch (nuevo)
        {
            case Parentesco.Hijo:
            {
                if (UnionComoHijo(id) != null) throw No(T("Ya tiene padres en el árbol."));
                Union destino;
                if (unionId != null) destino = Uniones.FirstOrDefault(x => x.Id == unionId && x.Parejas.Contains(otroId)) ?? throw No(T("Esa pareja ya no está en el árbol."));
                else
                {
                    var us = UnionesComoPareja(otroId).ToList();
                    if (us.Count > 1) throw No(T("Hay varias uniones: indica cuál."));
                    destino = us.Count == 1 ? us[0] : NuevaUnion(otroId);
                }
                destino.Hijos.Add(id);
                break;
            }
            case Parentesco.Hermano:
            {
                if (UnionComoHijo(id) != null) throw No(T("Ya tiene padres en el árbol."));
                if (!EsSanguinea(otroId)) throw No(T("La familia de una pareja política vive en su propio árbol."));
                var padres = UnionComoHijo(otroId);
                if (padres == null)
                {
                    var padre = NuevaPersona(Sexo.Hombre);
                    var madre = NuevaPersona(Sexo.Mujer);
                    padres = NuevaUnion(padre.Id, madre.Id);
                    padres.Hijos.Add(otroId);
                }
                padres.Hijos.Insert(padres.Hijos.IndexOf(otroId) + 1, id);
                break;
            }
            case Parentesco.Pareja:
            {
                if (UnionesComoPareja(otroId).Any(x => x.Parejas.Contains(id))) throw No(T("Ya son pareja."));
                if (suya != null) suya.Parejas.Add(otroId);
                else if (UnionesComoPareja(otroId).FirstOrDefault(x => x.Parejas.Count == 1) is { } libre) libre.Parejas.Add(id);
                else NuevaUnion(otroId, id);
                break;
            }
            case Parentesco.PadreOMadre:
            {
                if (!EsSanguinea(otroId)) throw No(T("La familia de una pareja política vive en su propio árbol."));
                var padres = UnionComoHijo(otroId);
                if (padres == null) { padres = NuevaUnion(id); padres.Hijos.Add(otroId); }
                else if (padres.Parejas.Contains(id)) throw No(T("Ya es así en el árbol."));
                else
                {
                    if (padres.Parejas.Count >= 2)
                    {
                        // uno de los dos puede ser una tarjeta vacía puesta al añadir los padres: se cambia por esta
                        var vacio = padres.Parejas.Select(Obtener).FirstOrDefault(p => p.SinDatos && SoloEsta(p.Id, padres))
                            ?? throw No(T("Ya tiene los dos padres."));
                        QuitarPersona(vacio, new List<Persona>());
                    }
                    padres.Parejas.Add(id);
                }
                break;
            }
        }

        // 4. Se limpia y se comprueba que lo que queda tiene sentido
        LimpiarUniones();
        if (HayCiclos()) throw No(T("No puede ser familiar de alguien de su propia rama."));
        if (encajaba && !GeneracionesCoherentes()) throw No(T("No encaja: quedaría en dos generaciones a la vez."));
        if (UnionComoHijo(id) != null && !EsSanguinea(id))
            throw No(T("Tiene padres en el árbol y una pareja política no puede tenerlos: su familia va en su propio árbol."));
        if (Firma() == antes) throw No(T("Ya es así en el árbol."));
        var quitadas = new List<Persona>();
        var alcanzables = Alcanzables();
        foreach (var x in Personas.Where(x => !alcanzables.Contains(x.Id)).ToList()) QuitarPersona(x, quitadas);
        LimpiarUniones();
        return new ResultadoMover(quitadas, acompanan);
    }

    /// <summary>
    /// Los padres que se crearon vacíos para colgar a un hermano (sin ningún dato ni más familia que esta unión) se quitan
    /// cuando se quedan con un hijo o ninguno: no aportan nada.
    /// </summary>
    private void QuitarPadresVacios(Union u)
    {
        if (u.Hijos.Count > 1 || u.Parejas.Count == 0) return;
        var padres = u.Parejas.Select(Obtener).ToList();
        if (!padres.All(p => p.SinDatos && SoloEsta(p.Id, u))) return;
        foreach (var p in padres) QuitarPersona(p, new List<Persona>());
        Uniones.Remove(u);
    }

    /// <summary>La persona no tiene más familia que la unión <paramref name="u"/> (en la que es pareja).</summary>
    private bool SoloEsta(string id, Union u) => UnionComoHijo(id) == null && UnionesComoPareja(id).All(x => x == u);

    /// <summary>Todo lo conectado con una persona (parejas, padres, hijos), ella incluida.</summary>
    private HashSet<string> AlcanzablesDesde(string id)
    {
        var res = new HashSet<string> { id };
        var cola = new Queue<string>();
        cola.Enqueue(id);
        while (cola.Count > 0)
        {
            var x = cola.Dequeue();
            foreach (var u in Uniones.Where(u => u.Parejas.Contains(x) || u.Hijos.Contains(x)))
                foreach (var q in u.Parejas.Concat(u.Hijos)) if (res.Add(q)) cola.Enqueue(q);
        }
        return res;
    }

    /// <summary>Alguien es antepasado de sí mismo.</summary>
    private bool HayCiclos()
    {
        var estado = new Dictionary<string, int>();   // 1: se está recorriendo; 2: terminado
        bool Visitar(string id)
        {
            if (estado.TryGetValue(id, out var e)) return e == 1;
            estado[id] = 1;
            if (UnionComoHijo(id) is { } u)
                foreach (var p in u.Parejas) if (Visitar(p)) return true;
            estado[id] = 2;
            return false;
        }
        return Personas.Any(p => Visitar(p.Id));
    }

    /// <summary>
    /// Cada persona está en una sola generación: las parejas en la misma y los hijos en la siguiente (no se es a la vez
    /// primo y padre de alguien). El dibujo lo necesita para poner a los padres encima de los hijos.
    /// </summary>
    private bool GeneracionesCoherentes()
    {
        var gen = new Dictionary<string, int>();
        foreach (var inicio in Personas)
        {
            if (gen.ContainsKey(inicio.Id)) continue;
            gen[inicio.Id] = 0;
            var cola = new Queue<string>();
            cola.Enqueue(inicio.Id);
            while (cola.Count > 0)
            {
                var x = cola.Dequeue();
                foreach (var u in Uniones.Where(u => u.Parejas.Contains(x) || u.Hijos.Contains(x)))
                {
                    int g = u.Parejas.Contains(x) ? gen[x] : gen[x] - 1;
                    foreach (var q in u.Parejas.Select(q => (q, g)).Concat(u.Hijos.Select(h => (q: h, g: g + 1))))
                    {
                        if (gen.TryGetValue(q.q, out var ya)) { if (ya != q.g) return false; }
                        else { gen[q.q] = q.g; cola.Enqueue(q.q); }
                    }
                }
            }
        }
        return true;
    }

    /// <summary>Las relaciones del árbol (quién es pareja de quién y sus hijos), sin fijarse en ids de uniones ni en el orden.</summary>
    private string Firma() => string.Join("|", Uniones
        .Select(u => string.Join(",", u.Parejas.Order()) + ">" + string.Join(",", u.Hijos.Order()))
        .Order());
}
