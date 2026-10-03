using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

/// <summary>Familias de prueba: a mano (casos típicos) y aleatorias respetando los invariantes del modelo.</summary>
public static class Familias
{
    public static Persona Nombrar(this Arbol a, string id, string nombre, string apellidos = "", Sexo? sexo = null)
    {
        var p = a.Obtener(id);
        p.Nombre = nombre; p.Apellidos = apellidos;
        if (sexo != null) p.Sexo = sexo.Value;
        return p;
    }

    /// <summary>Antepasado de la principal a 2 o más generaciones (abuelos, bisabuelos...).</summary>
    public static bool EsAntepasadoLejano(Arbol a, string id)
    {
        var padres = a.UnionComoHijo(a.RaizId)?.Parejas ?? new List<string>();
        var cola = new Queue<string>(padres);
        var vistos = new HashSet<string>(padres);
        while (cola.Count > 0)
        {
            var u = a.UnionComoHijo(cola.Dequeue());
            if (u == null) continue;
            foreach (var q in u.Parejas) if (vistos.Add(q)) cola.Enqueue(q);
        }
        vistos.ExceptWith(padres);
        return vistos.Contains(id);
    }

    public static Arbol Aleatoria(int semilla, int pasos, int maxParejas = 3, bool hermanosDeAntepasados = false)
    {
        var rnd = new Random(semilla);
        var a = Arbol.Nuevo("Aleatoria " + semilla, "Raíz");
        a.Obtener(a.RaizId).Sexo = rnd.Next(2) == 0 ? Sexo.Hombre : Sexo.Mujer;
        for (int i = 0; i < pasos; i++)
        {
            var p = a.Personas[rnd.Next(a.Personas.Count)];
            bool sang = a.EsSanguinea(p.Id);
            switch (rnd.Next(10))
            {
                case 0 or 1:
                    if (sang && !a.TienePadresCompletos(p.Id)) a.AnadirPadres(p.Id);
                    break;
                case 2 or 3:
                    // Los hermanos de antepasados lejanos (tíos abuelos...) pueden forzar cruces: se miden aparte.
                    if (sang && (hermanosDeAntepasados || !EsAntepasadoLejano(a, p.Id))) a.AnadirHermano(p.Id);
                    break;
                case 4 or 5:
                    if (a.UnionesComoPareja(p.Id).Count() < maxParejas) a.AnadirPareja(p.Id);
                    break;
                default:
                    var us = a.UnionesComoPareja(p.Id).ToList();
                    var elegida = us.Count > 1 ? us[rnd.Next(us.Count)] : us.FirstOrDefault();
                    // Un hijo nuevo es hermano de los demás hijos de la unión (ver comentario de los hermanos).
                    if (!hermanosDeAntepasados && elegida != null && elegida.Hijos.Any(h => EsAntepasadoLejano(a, h))) break;
                    a.AnadirHijo(p.Id, elegida?.Id);
                    break;
            }
        }
        int n = 1;
        foreach (var p in a.Personas) { if (p.Nombre == "") p.Nombre = "P" + n; n++; }
        // sexos aleatorios (las parejas ya vienen con sexo opuesto cuando hay dato)
        foreach (var p in a.Personas.Where(p => p.Sexo == Sexo.Desconocido))
            p.Sexo = rnd.Next(5) == 0 ? Sexo.Desconocido : (rnd.Next(2) == 0 ? Sexo.Hombre : Sexo.Mujer);
        return a;
    }

    /// <summary>Yo, mis padres, ambos abuelos, tíos con pareja e hijos, mi pareja, mis hijos y nietos.</summary>
    public static Arbol Tipica()
    {
        var a = Arbol.Nuevo("Típica", "Yo");
        var yo = a.RaizId;
        a.Nombrar(yo, "Yo", "García López", Sexo.Hombre);
        var padres = a.AnadirPadres(yo);
        a.Nombrar(padres[0].Id, "Padre", "García", Sexo.Hombre);
        a.Nombrar(padres[1].Id, "Madre", "López", Sexo.Mujer);
        var h1 = a.AnadirHermano(yo).hermano; a.Nombrar(h1.Id, "Hermana", "García López", Sexo.Mujer);
        var h2 = a.AnadirHermano(h1.Id).hermano; a.Nombrar(h2.Id, "Hermano menor", "García López", Sexo.Hombre);

        // abuelos paternos con 3 hijos (padre + 2 tíos)
        var ap = a.AnadirPadres(padres[0].Id);
        a.Nombrar(ap[0].Id, "Abuelo paterno", "García", Sexo.Hombre); a.Nombrar(ap[1].Id, "Abuela paterna", "Ruiz", Sexo.Mujer);
        var tio1 = a.AnadirHermano(padres[0].Id).hermano; a.Nombrar(tio1.Id, "Tío Juan", "García Ruiz", Sexo.Hombre);
        var tio2 = a.AnadirHermano(tio1.Id).hermano; a.Nombrar(tio2.Id, "Tía Marta", "García Ruiz", Sexo.Mujer);
        var tia = a.AnadirPareja(tio1.Id); a.Nombrar(tia.Id, "Tía Rosa", "Pérez", Sexo.Mujer);
        for (int i = 1; i <= 3; i++)
        {
            var pr = a.AnadirHijo(tio1.Id); a.Nombrar(pr.Id, "Primo " + i, "García Pérez");
            if (i == 2) { var nieto = a.AnadirHijo(pr.Id); a.Nombrar(nieto.Id, "Sobrino segundo", "García"); }
        }
        var marido = a.AnadirPareja(tio2.Id); a.Nombrar(marido.Id, "Tío Pedro", "Sanz", Sexo.Hombre);
        var pm = a.AnadirHijo(tio2.Id); a.Nombrar(pm.Id, "Prima Lucía", "Sanz García", Sexo.Mujer);

        // abuelos maternos con 2 hijas
        var am = a.AnadirPadres(padres[1].Id);
        a.Nombrar(am[0].Id, "Abuelo materno", "López", Sexo.Hombre); a.Nombrar(am[1].Id, "Abuela materna", "Gil", Sexo.Mujer);
        var tiaM = a.AnadirHermano(padres[1].Id).hermano; a.Nombrar(tiaM.Id, "Tía Elena", "López Gil", Sexo.Mujer);
        for (int i = 1; i <= 2; i++) { var pr = a.AnadirHijo(tiaM.Id); a.Nombrar(pr.Id, "Primo materno " + i, "López"); }

        // bisabuelos por parte de la abuela materna
        var bis = a.AnadirPadres(am[1].Id);
        a.Nombrar(bis[0].Id, "Bisabuelo", "Gil", Sexo.Hombre); a.Nombrar(bis[1].Id, "Bisabuela", "Toro", Sexo.Mujer);

        // mi pareja e hijos
        var pareja = a.AnadirPareja(yo); a.Nombrar(pareja.Id, "Mi esposa", "Martín", Sexo.Mujer);
        for (int i = 1; i <= 2; i++)
        {
            var hijo = a.AnadirHijo(yo); a.Nombrar(hijo.Id, "Hijo " + i, "García Martín");
            if (i == 1) { var nietos = a.AnadirHijo(hijo.Id); a.Nombrar(nietos.Id, "Nieto", "García"); }
        }
        // mi hermana con pareja e hijos
        var cuñado = a.AnadirPareja(h1.Id); a.Nombrar(cuñado.Id, "Cuñado", "Vega", Sexo.Hombre);
        for (int i = 1; i <= 3; i++) { var sob = a.AnadirHijo(h1.Id); a.Nombrar(sob.Id, "Sobrino " + i, "Vega García"); }
        return a;
    }

    /// <summary>Persona con dos matrimonios y descendencia en cada uno.</summary>
    public static Arbol SegundasParejas()
    {
        var a = Arbol.Nuevo("Segundas parejas", "Carlos");
        var c = a.RaizId; a.Nombrar(c, "Carlos", "Ortiz", Sexo.Hombre);
        var m1 = a.AnadirPareja(c); a.Nombrar(m1.Id, "Ana", "Ruiz", Sexo.Mujer);
        var u1 = a.UnionesComoPareja(c).First();
        for (int i = 1; i <= 3; i++) { var h = a.AnadirHijo(c, u1.Id); a.Nombrar(h.Id, "Hijo A" + i, "Ortiz Ruiz"); a.AnadirHijo(h.Id); }
        var m2 = a.AnadirPareja(c); a.Nombrar(m2.Id, "Berta", "Sola", Sexo.Mujer);
        var u2 = a.UnionesComoPareja(c).Last();
        for (int i = 1; i <= 2; i++) { var h = a.AnadirHijo(c, u2.Id); a.Nombrar(h.Id, "Hijo B" + i, "Ortiz Sola"); }
        var pad = a.AnadirPadres(c); a.Nombrar(pad[0].Id, "Padre", "Ortiz", Sexo.Hombre); a.Nombrar(pad[1].Id, "Madre", "Gómez", Sexo.Mujer);
        var her = a.AnadirHermano(c).hermano; a.Nombrar(her.Id, "Hermano", "Ortiz Gómez", Sexo.Hombre);
        var cu = a.AnadirPareja(her.Id); a.Nombrar(cu.Id, "Cuñada", "Mora", Sexo.Mujer);
        for (int i = 1; i <= 3; i++) { var h = a.AnadirHijo(her.Id); a.Nombrar(h.Id, "Sobrino " + i, "Ortiz Mora"); }
        return a;
    }
}
