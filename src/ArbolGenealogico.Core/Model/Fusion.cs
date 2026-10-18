using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Core.Model;

/// <summary>Lo que ha hecho (o haría) una fusión.</summary>
public sealed class InformeFusion
{
    /// <summary>Personas del otro árbol que no estaban en este y se han añadido.</summary>
    public int Nuevas { get; set; }
    /// <summary>Personas que ya estaban y a las que se les ha completado algún dato (foto, historia…).</summary>
    public int Completadas { get; set; }
    /// <summary>Personas que están en los dos árboles.</summary>
    public int Comunes { get; set; }
    /// <summary>Personas que están en los dos con algún dato distinto (y cuál se ha quedado depende de la preferencia).</summary>
    public List<string> ConDiferencias { get; } = new();
    /// <summary>De las personas nuevas, las que no quedan unidas a la persona principal (no se verían).</summary>
    public int Sueltas { get; set; }
    public List<string> Avisos { get; } = new();

    /// <summary>La fusión cambiaría algo: trae personas o datos nuevos, o datos distintos entre los que elegir.</summary>
    public bool AportaAlgo => Nuevas > 0 || Completadas > 0 || ConDiferencias.Count > 0;
}

/// <summary>Una persona del otro árbol que parece ser una de este (mismo nombre y familia parecida).</summary>
public sealed record Parecido(string IdMio, string IdSuyo, int Puntos);

/// <summary>
/// Fusión de árboles: lo que permite que la familia se mande los árboles y los junte sin ningún servidor. Las personas son
/// la misma cuando tienen el mismo id (vienen de una copia del mismo árbol) o cuando así se indica (las que no comparten
/// id, por ejemplo dos árboles empezados por separado). Se añaden las personas y uniones que faltan y se completan los
/// datos vacíos; si un dato está en los dos y es distinto, se queda el de este árbol salvo que se prefiera el otro.
/// </summary>
public static class Fusion
{
    /// <summary>
    /// Añade <paramref name="otro"/> a <paramref name="mio"/> (que cambia). <paramref name="equivalencias"/>: id del otro
    /// árbol → id de este, para las personas que son la misma sin compartir id.
    /// </summary>
    public static InformeFusion Fusionar(Arbol mio, Arbol otro, IReadOnlyDictionary<string, string>? equivalencias = null, bool preferirOtro = false)
    {
        var inf = new InformeFusion();
        var suyo = otro.Clonar();
        var antes = mio.Personas.Select(p => p.Id).ToHashSet();

        // 1. Los ids del otro árbol pasan a los de este según las equivalencias (sin juntar dos personas suyas en una mía).
        var mapa = new Dictionary<string, string>();
        var usados = new HashSet<string>();
        foreach (var p in suyo.Personas)
        {
            string destino = p.Id;
            if (equivalencias != null && equivalencias.TryGetValue(p.Id, out var eq) && mio.Buscar(eq) != null && !usados.Contains(eq)
                && !suyo.Personas.Any(q => q.Id == eq && q != p))
                destino = eq;
            if (!usados.Add(destino)) { destino = Arbol.NuevoId(); usados.Add(destino); }
            mapa[p.Id] = destino;
        }
        foreach (var p in suyo.Personas) p.Id = mapa[p.Id];
        foreach (var u in suyo.Uniones)
        {
            u.Parejas = u.Parejas.Select(x => mapa.GetValueOrDefault(x, x)).ToList();
            u.Hijos = u.Hijos.Select(x => mapa.GetValueOrDefault(x, x)).ToList();
        }

        // 2. Personas: las que ya están se completan; las demás se añaden.
        foreach (var ps in suyo.Personas)
        {
            var pm = mio.Buscar(ps.Id);
            if (pm == null) { mio.Personas.Add(ps.Clonar()); inf.Nuevas++; continue; }
            inf.Comunes++;
            bool completada = false, distinta = false;
            string Unir(string mioValor, string suyoValor)
            {
                if (string.IsNullOrWhiteSpace(suyoValor) || mioValor.Trim() == suyoValor.Trim()) return mioValor;
                if (string.IsNullOrWhiteSpace(mioValor)) { completada = true; return suyoValor; }
                distinta = true;
                return preferirOtro ? suyoValor : mioValor;
            }
            pm.Nombre = Unir(pm.Nombre, ps.Nombre);
            pm.Apellidos = Unir(pm.Apellidos, ps.Apellidos);
            pm.Historia = Unir(pm.Historia, ps.Historia);
            pm.Foto = NuloSiVacio(Unir(pm.Foto ?? "", ps.Foto ?? ""));
            pm.ArbolEnlazado = NuloSiVacio(Unir(pm.ArbolEnlazado ?? "", ps.ArbolEnlazado ?? ""));
            // las fechas: si las dos dicen lo mismo y una sabe más (1920 y el 5 de marzo de 1920), se queda la que sabe más
            Fecha? UnirFecha(Fecha? mia, Fecha? suya)
            {
                if (suya is not { } s || mia == suya) return mia;
                if (mia is not { } m) { completada = true; return s; }
                if (Compatibles(m, s))
                {
                    if (Precision(s) > Precision(m)) { completada = true; return s; }
                    return m;
                }
                distinta = true;
                return preferirOtro ? s : m;
            }
            pm.Nacimiento = UnirFecha(pm.Nacimiento, ps.Nacimiento);
            pm.Defuncion = UnirFecha(pm.Defuncion, ps.Defuncion);
            if (ps.Sexo != Sexo.Desconocido && ps.Sexo != pm.Sexo)
            {
                if (pm.Sexo == Sexo.Desconocido) { pm.Sexo = ps.Sexo; completada = true; }
                else { distinta = true; if (preferirOtro) pm.Sexo = ps.Sexo; }
            }
            if (completada) inf.Completadas++;
            if (distinta) inf.ConDiferencias.Add(pm.Id);
        }

        // 3. Uniones: la misma (por id, por la misma pareja o por compartir hijos) se completa; si no, se añade.
        foreach (var us in suyo.Uniones)
        {
            var um = mio.Uniones.FirstOrDefault(u => u.Id == us.Id)
                  ?? (us.Parejas.Count > 0 ? mio.Uniones.FirstOrDefault(u => u.Parejas.Count == us.Parejas.Count && u.Parejas.All(us.Parejas.Contains)) : null)
                  ?? mio.Uniones.FirstOrDefault(u => u.Hijos.Any(us.Hijos.Contains) && u.Parejas.Union(us.Parejas).Count() <= 2);
            if (um == null)
            {
                um = new Union { Id = mio.Uniones.Any(u => u.Id == us.Id) ? Arbol.NuevoId() : us.Id, Parejas = new(us.Parejas) };
                mio.Uniones.Add(um);
            }
            else
            {
                foreach (var q in us.Parejas.Where(q => !um.Parejas.Contains(q)))
                {
                    if (um.Parejas.Count < 2) um.Parejas.Add(q);
                    else inf.Avisos.Add(T("Una pareja tenía distinto cónyuge en los dos árboles: se ha dejado la de este."));
                }
            }
            // los hijos nuevos, en su orden: detrás del hermano anterior que ya estuviera
            int pos = -1;
            foreach (var h in us.Hijos)
            {
                int i = um.Hijos.IndexOf(h);
                if (i >= 0) { pos = i; continue; }
                if (mio.Uniones.Any(u => u != um && u.Hijos.Contains(h)))
                {
                    inf.Avisos.Add(T("{0} tenía distintos padres en los dos árboles: se han dejado los de este.", Mostrar(mio.Buscar(h))));
                    continue;
                }
                um.Hijos.Insert(pos + 1, h);
                pos++;
            }
        }

        // una unión que solo trae a una persona sin hijos (porque sus hijos ya tenían padres aquí) no aporta nada
        mio.Uniones.RemoveAll(u => u.Parejas.Count <= 1 && u.Hijos.Count == 0);
        mio.Reparar();
        var alcanzables = mio.Alcanzables();
        inf.Sueltas = mio.Personas.Count(p => !antes.Contains(p.Id) && !alcanzables.Contains(p.Id));
        return inf;
    }

    /// <summary>Lo que haría la fusión, sin cambiar nada.</summary>
    public static InformeFusion Probar(Arbol mio, Arbol otro, IReadOnlyDictionary<string, string>? equivalencias = null, bool preferirOtro = false) =>
        Fusionar(mio.Clonar(), otro, equivalencias, preferirOtro);

    /// <summary>
    /// Personas del otro árbol que no comparten id con ninguna de este y parecen la misma que una de este: mismo nombre
    /// y apellidos (sin fijarse en tildes ni mayúsculas), sin sexo contradictorio, y mejor aún si sus familiares también
    /// coinciden. Cada persona sale como mucho una vez; de más a menos parecido.
    /// </summary>
    public static List<Parecido> Parecidos(Arbol mio, Arbol otro)
    {
        var mias = mio.Personas.Where(p => !Nombre(p).Equals("")).ToList();
        var comunes = otro.Personas.Select(p => p.Id).Where(id => mio.Buscar(id) != null).ToHashSet();
        var candidatos = new List<Parecido>();
        foreach (var ps in otro.Personas.Where(p => !comunes.Contains(p.Id) && Nombre(p) != ""))
        {
            foreach (var pm in mias.Where(p => !comunes.Contains(p.Id)))
            {
                if (ps.Sexo != Sexo.Desconocido && pm.Sexo != Sexo.Desconocido && ps.Sexo != pm.Sexo) continue;
                int puntos = Nombre(ps) == Nombre(pm) ? 100
                    : Texto.Normalizar(ps.Nombre).Trim() == Texto.Normalizar(pm.Nombre).Trim() && PrimerApellido(ps) != "" && PrimerApellido(ps) == PrimerApellido(pm) ? 80
                    : 0;
                if (puntos == 0) continue;
                // familiares con el mismo nombre (padres, parejas, hijos)
                var fs = Familiares(otro, ps.Id).Select(Nombre).Where(n => n != "").ToHashSet();
                var fm = Familiares(mio, pm.Id).Select(Nombre).Where(n => n != "").ToHashSet();
                puntos += 15 * Math.Min(4, fs.Count(fm.Contains));
                candidatos.Add(new Parecido(pm.Id, ps.Id, puntos));
            }
        }
        var res = new List<Parecido>();
        var usadosMios = new HashSet<string>(); var usadosSuyos = new HashSet<string>();
        foreach (var c in candidatos.OrderByDescending(c => c.Puntos))
            if (!usadosMios.Contains(c.IdMio) && !usadosSuyos.Contains(c.IdSuyo))
            {
                res.Add(c); usadosMios.Add(c.IdMio); usadosSuyos.Add(c.IdSuyo);
            }
        return res;
    }

    private static IEnumerable<Persona> Familiares(Arbol a, string id)
    {
        var ids = new List<string>();
        if (a.UnionComoHijo(id) is { } padres) ids.AddRange(padres.Parejas);
        foreach (var u in a.UnionesComoPareja(id)) { ids.AddRange(u.Parejas.Where(x => x != id)); ids.AddRange(u.Hijos); }
        return ids.Distinct().Select(a.Buscar).Where(p => p != null)!;
    }

    private static string Nombre(Persona p) => string.Join(' ', Texto.Normalizar(p.Nombre + " " + p.Apellidos).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string PrimerApellido(Persona p) => Texto.Normalizar(p.Apellidos).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    private static string? NuloSiVacio(string s) => string.IsNullOrEmpty(s) ? null : s;

    /// <summary>Dos fechas que no se contradicen: el mismo año y, donde las dos lo saben, el mismo mes y día.</summary>
    private static bool Compatibles(Fecha a, Fecha b) =>
        a.Anio == b.Anio && (a.Mes is null || b.Mes is null || a.Mes == b.Mes) && (a.Dia is null || b.Dia is null || a.Dia == b.Dia);

    /// <summary>Lo que sabe una fecha: más con mes y día, y algo más si no es aproximada.</summary>
    private static int Precision(Fecha f) => (f.Mes != null ? 2 : 0) + (f.Dia != null ? 2 : 0) + (f.Aproximada ? 0 : 1);
    private static string Mostrar(Persona? p) => p == null ? T("Alguien") : string.IsNullOrWhiteSpace(p.NombreCompleto) ? T("Una persona sin nombre") : p.NombreCompleto;
}
