using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// Cambiar el parentesco de quien se puso donde no era (el hermano metido como hermano del padre): se elige a la persona
/// de la que es familiar, se dice qué es de ella y, tras ver lo que va a cambiar, se cambia. Lo usan el teléfono (se toca
/// a la otra persona en el árbol) y el ordenador.
/// </summary>
public static class CambioParentesco
{
    /// <param name="elegir">Espera a que se elija a la otra persona (null si se cancela).</param>
    /// <param name="cambiar">Aplica el cambio al árbol (con deshacer).</param>
    /// <returns>Si se ha cambiado.</returns>
    public static async Task<bool> Hacer(Arbol arbol, string id, Func<Task<string?>> elegir, Action<Action<Arbol>> cambiar)
    {
        if (arbol.Buscar(id) is not { } p || arbol.PorQueNoSeMueve(id) != null) return false;
        var otroId = await elegir();
        if (otroId == null || arbol.Buscar(otroId) is not { } otro) return false;
        Union UnionDe(string uid) => arbol.Uniones.First(u => u.Id == uid);

        // lo que se puede hacer: qué vínculo se quita, con qué pareja de la otra persona (si es hijo) y si se lleva a sus hijos
        var vinculos = arbol.Vinculos(id);
        List<(Vinculo V, string? Union, bool ConElla)> Formas(Parentesco que)
        {
            var vs = (que is Parentesco.Hijo or Parentesco.Hermano) && vinculos.FirstOrDefault(v => v.ComoHijo) is { } padres ? new List<Vinculo> { padres } : vinculos;
            var uniones = que == Parentesco.Hijo ? arbol.UnionesComoPareja(otroId).Select(u => (string?)u.Id).DefaultIfEmpty(null).ToList() : new List<string?> { null };
            return (from v in vs from u in uniones from c in new[] { false, true }
                    where !c || (!v.ComoHijo && UnionDe(v.UnionId).Hijos.Count > 0)
                    select (v, u, c)).ToList();
        }
        var errores = new Dictionary<Parentesco, string>();
        var posibles = new Dictionary<Parentesco, List<(Vinculo V, string? Union, bool ConElla)>>();
        foreach (var que in Enum.GetValues<Parentesco>())
        {
            var bien = new List<(Vinculo, string?, bool)>();
            foreach (var f in Formas(que))
            {
                var (_, error) = arbol.ProbarMover(id, f.V, que, otroId, f.Union, f.ConElla);
                if (error == null) bien.Add(f);
                else errores.TryAdd(que, error);
            }
            posibles[que] = bien;
        }
        var textos = new (Parentesco Que, string Texto)[]
        {
            (Parentesco.PadreOMadre, T("Padre o madre")), (Parentesco.Hermano, T("Hermano o hermana")),
            (Parentesco.Pareja, T("Pareja")), (Parentesco.Hijo, T("Hijo o hija")),
        };
        var ops = textos.Select(t => new Opcion(t.Texto, Ui.Iconos.Persona, Activa: posibles[t.Que].Count > 0,
            Detalle: posibles[t.Que].Count > 0 ? null : errores.GetValueOrDefault(t.Que))).ToList();
        var r = await Principal.Actual.Menu(T("¿Qué es {0} de {1}?", Nombres.Mostrar(p), Nombres.Mostrar(otro)), ops);
        if (r == null) return false;
        var nuevo = textos[ops.IndexOf(r)].Que;
        var formas = posibles[nuevo];

        // hijo de qué pareja de la otra persona
        if (formas.Select(f => f.Union).Distinct().Count() > 1)
        {
            var us = formas.Select(f => f.Union!).Distinct().ToList();
            var opsU = us.Select(u =>
            {
                var pareja = UnionDe(u).Parejas.FirstOrDefault(x => x != otroId);
                return new Opcion(T("Con {0}", Nombres.Mostrar(pareja == null ? null : arbol.Buscar(pareja))), Ui.Iconos.Persona);
            }).ToList();
            var e = await Principal.Actual.Menu(T("¿Hijo o hija de qué pareja?"), opsU);
            if (e == null) return false;
            var elegida = us[opsU.IndexOf(e)];
            formas = formas.Where(f => f.Union == elegida).ToList();
        }
        // qué parentesco se cambia (si tiene varios)
        if (formas.Select(f => f.V).Distinct().Count() > 1)
        {
            var vs = formas.Select(f => f.V).Distinct().ToList();
            var opsV = vs.Select(v => new Opcion(Describir(arbol, p, v), Ui.Iconos.Persona)).ToList();
            var e = await Principal.Actual.Menu(T("¿Qué parentesco se cambia?"), opsV);
            if (e == null) return false;
            var elegido = vs[opsV.IndexOf(e)];
            formas = formas.Where(f => f.V == elegido).ToList();
        }
        // sus hijos con la pareja que deja: se quedan con ella o se van con esta persona
        if (formas.Select(f => f.ConElla).Distinct().Count() > 1)
        {
            var otraPareja = UnionDe(formas[0].V.UnionId).Parejas.First(x => x != id);
            var quedan = new Opcion(T("Se quedan con {0}", Nombres.Mostrar(arbol.Buscar(otraPareja))), Ui.Iconos.Persona);
            var van = new Opcion(T("Se van con {0}", Nombres.Mostrar(p)), Ui.Iconos.Persona);
            var e = await Principal.Actual.Menu(T("¿Y sus hijos?"), new[] { quedan, van });
            if (e == null) return false;
            formas = formas.Where(f => f.ConElla == (e == van)).ToList();
        }
        var (vinculo, union, conElla) = formas[0];
        var (res, _) = arbol.ProbarMover(id, vinculo, nuevo, otroId, union, conElla);
        if (res == null) return false;

        var texto = T("Antes: {0}", Describir(arbol, p, vinculo)) + "\n" + T("Ahora: {0}", DescribirNuevo(arbol, p, nuevo, otro, union));
        if (res.Acompanan > 0)
            texto += "\n\n" + N(res.Acompanan, "También cambia de sitio su familia: 1 persona más.", "También cambia de sitio su familia: {0} personas más.");
        int quitadas = res.Quitadas.Count(x => !x.SinDatos);
        if (quitadas > 0)
            texto += "\n\n" + N(quitadas, "Se quitará 1 persona que solo estaba unida al árbol por este parentesco.",
                "Se quitarán {0} personas que solo estaban unidas al árbol por este parentesco.");
        texto += "\n\n" + T("Se puede deshacer en el menú.");
        if (await Principal.Actual.Preguntar(T("¿Cambiar el parentesco de {0}?", Nombres.Mostrar(p)), texto, T("Cambiar"), T("Cancelar")) != 0) return false;
        cambiar(a => a.Mover(id, vinculo, nuevo, otroId, union, conElla));
        return true;
    }

    /// <summary>«Ana y Luis», «Ana y 3 más».</summary>
    private static string Lista(IReadOnlyList<string> nombres) => nombres.Count switch
    {
        0 => "",
        1 => nombres[0],
        2 => T("{0} y {1}", nombres[0], nombres[1]),
        _ => T("{0} y {1}", nombres[0], N(nombres.Count - 1, "1 más", "{0} más")),
    };

    /// <summary>Lo que es ahora la persona por ese vínculo: «Hijo de Ana y Luis», «Hermana de Pepe», «Padre de Lucía»…</summary>
    private static string Describir(Arbol arbol, Persona p, Vinculo v)
    {
        string Nombre(string x) => Nombres.Mostrar(arbol.Buscar(x));
        var u = arbol.Uniones.First(x => x.Id == v.UnionId);
        if (v.ComoHijo)
        {
            var conNombre = u.Parejas.Where(x => arbol.Buscar(x) is { } q && !Nombres.SinNombre(q)).ToList();
            var hermanos = u.Hijos.Where(x => x != p.Id && arbol.Buscar(x) is { } q && !Nombres.SinNombre(q)).ToList();
            // con los padres sin nombre (puestos al añadir un hermano), se entiende mejor por los hermanos
            if (conNombre.Count == 0 && hermanos.Count > 0) return T("Hermano de {0}", p.Sexo, Lista(hermanos.Select(Nombre).ToList()));
            return T("Hijo de {0}", p.Sexo, Lista(u.Parejas.Select(Nombre).ToList()));
        }
        if (u.Hijos.Count > 0) return T("Padre de {0}", p.Sexo, Lista(u.Hijos.Select(Nombre).ToList()));
        return T("Pareja de {0}", Nombre(u.Parejas.First(x => x != p.Id)));
    }

    /// <summary>Lo que va a ser la persona de la otra.</summary>
    private static string DescribirNuevo(Arbol arbol, Persona p, Parentesco que, Persona otro, string? union) => que switch
    {
        Parentesco.PadreOMadre => T("Padre de {0}", p.Sexo, Nombres.Mostrar(otro)),
        Parentesco.Hermano => T("Hermano de {0}", p.Sexo, Nombres.Mostrar(otro)),
        Parentesco.Pareja => T("Pareja de {0}", Nombres.Mostrar(otro)),
        _ => T("Hijo de {0}", p.Sexo, Lista((union == null ? new List<string> { otro.Id } : arbol.Uniones.First(x => x.Id == union).Parejas).Select(x => Nombres.Mostrar(arbol.Buscar(x))).ToList())),
    };
}
