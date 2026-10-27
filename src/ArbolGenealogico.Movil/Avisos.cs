using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using ArbolGenealogico.Movil.Vistas;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil;

/// <summary>Un aviso del sistema: su título, sus líneas y la persona que se abre al tocarlo.</summary>
public sealed record AvisoCumple(int Dias, string Titulo, string Texto, Fiesta Primera);

/// <summary>
/// Los avisos de cumpleaños (en Android, una notificación por la mañana; ver <see cref="IPlataforma.ProgramarAvisos"/>):
/// de todos los árboles del teléfono menos los de ejemplo, el mismo día y, si se quiere, también unos días antes.
/// </summary>
public static class Avisos
{
    /// <summary>Las horas a las que se puede avisar.</summary>
    private static readonly int[] Horas = { 9, 10, 12, 19 };

    private static bool EsEjemplo(EntradaArbol e) =>
        e.Archivo.StartsWith("familia-perez", StringComparison.OrdinalIgnoreCase) || e.Archivo.StartsWith("familia-garcia", StringComparison.OrdinalIgnoreCase);

    /// <summary>Los árboles de la biblioteca que pueden avisar, el último cambiado primero: sin los de ejemplo (sus personas no existen).</summary>
    private static List<EntradaArbol> Entradas() => App.Biblioteca.Listar().Where(e => !EsEjemplo(e)).ToList();

    private static bool ConAvisos(EntradaArbol e) => !Preferencias.Actual.ArbolesSinAvisos.Contains(e.Archivo, StringComparer.OrdinalIgnoreCase);

    /// <summary>Los árboles de los que se avisa (sin los de ejemplo ni los que se han quitado en el calendario).</summary>
    public static IEnumerable<(string Ruta, Arbol Arbol)> Arboles()
    {
        foreach (var e in Entradas().Where(ConAvisos))
        {
            Arbol? a = null;
            try { a = ArbolJson.Cargar(e.Ruta, out _); } catch { }
            if (a != null) yield return (e.Ruta, a);
        }
    }

    /// <summary>Si hay algún árbol de verdad (sin contar los de ejemplo).</summary>
    public static bool HayArboles() => Entradas().Count > 0;

    /// <summary>Al cambiar el nombre de un árbol (y de su archivo), si no avisaba, sigue sin avisar.</summary>
    public static void Renombrado(string archivoViejo, string archivoNuevo)
    {
        var lista = Preferencias.Actual.ArbolesSinAvisos;
        if (lista.RemoveAll(a => string.Equals(a, archivoViejo, StringComparison.OrdinalIgnoreCase)) == 0) return;
        lista.Add(archivoNuevo);
        Preferencias.Actual.Guardar();
    }

    /// <summary>Lo que hay que avisar <paramref name="hoy"/>: los de hoy y, si se quiere, los de dentro de unos días.</summary>
    public static List<AvisoCumple> DeHoy(DateTime hoy)
    {
        var pref = Preferencias.Actual;
        var arboles = Arboles().ToList();
        var dias = pref.DiasAntesAvisos > 0 ? new[] { 0, pref.DiasAntesAvisos } : new[] { 0 };
        var avisos = new List<AvisoCumple>();
        foreach (var d in dias)
        {
            var fiestas = Cumpleanos.Del(hoy.Date.AddDays(d), arboles, pref.AvisosAniversarios, pref.AvisosBodas);
            if (fiestas.Count > 0) avisos.Add(new AvisoCumple(d, Cumpleanos.Cuando(d), string.Join("\n", fiestas.Select(f => f.Frase)), fiestas[0]));
        }
        return avisos;
    }

    /// <summary>Programa (o quita) el aviso de cada día según los ajustes. Se hace cada vez que se abre la aplicación.</summary>
    public static void Programar()
    {
        if (!App.Plataforma.HayAvisos) return;
        var pref = Preferencias.Actual;
        try { App.Plataforma.ProgramarAvisos(pref.AvisosCumple ? pref.HoraAvisos : null); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Avisos: " + ex.Message); }
    }

    /// <summary>
    /// La primera vez que hay un árbol (y Android pide permiso para avisar, desde Android 13), se pregunta si se quieren
    /// los avisos. Solo una vez: si se dice que no, se pueden activar después con el calendario de «Mis árboles».
    /// </summary>
    public static async Task PreguntarSiHaceFalta()
    {
        var pref = Preferencias.Actual;
        if (!App.Plataforma.HayAvisos || pref.PermisoAvisosPreguntado || !pref.AvisosCumple || App.Plataforma.AvisosPermitidos
            || Principal.Actual.HayDialogo || !HayArboles()) return;
        pref.PermisoAvisosPreguntado = true;
        pref.Guardar();
        int r = await Principal.Actual.Preguntar(T("¿Te aviso de los cumpleaños?"),
            T("Ramo puede avisarte por la mañana cuando alguien de tu familia cumpla años. Funciona sin internet y lo puedes cambiar cuando quieras con el calendario de «Mis árboles»."),
            T("Sí, avisarme"), T("Ahora no"));
        pref.AvisosCumple = r == 0 && await App.Plataforma.PedirPermisoAvisos();
        pref.Guardar();
        Programar();
    }

    /// <summary>
    /// Los ajustes de los avisos (el calendario de «Mis árboles»): si se quieren, a qué hora, cuándo y si también los
    /// aniversarios de las muertes y de las bodas; y la lista de los próximos cumpleaños.
    /// </summary>
    public static async Task Ajustes()
    {
        var p = Principal.Actual;
        while (true)
        {
            var pref = Preferencias.Actual;
            Opcion si = null!, proximos = null!;
            static Opcion Ajuste(string texto, string? icono, bool marcada, bool activa, Action cambiar) =>
                new(texto, icono, Marcada: marcada, Activa: activa) { AlElegir = cambiar };
            void Cambiar(Action cambio) { cambio(); pref.Guardar(); Programar(); }
            IReadOnlyList<Opcion> Todas()
            {
                bool activos = pref.AvisosCumple && App.Plataforma.AvisosPermitidos;
                // «Sí» puede tener que pedir permiso: cierra el menú (y se vuelve a abrir después)
                si = new Opcion(T("Sí"), Ui.Iconos.CalendarioTarta, Marcada: activos);
                proximos = new Opcion(T("Ver los próximos cumpleaños"), Ui.Iconos.Documento);
                // con varios árboles, se puede elegir de cuáles se avisa (p. ej., no del de una familia lejana)
                var entradas = Entradas();
                var arboles = entradas.Count < 2 ? new List<Opcion>() : entradas.Select(e =>
                {
                    bool con = ConAvisos(e);
                    return Ajuste(e.Nombre, con ? Ui.Iconos.CalendarioTarta : Ui.Iconos.SinCalendarioTarta, con, activos, () => Cambiar(() =>
                    {
                        var lista = pref.ArbolesSinAvisos;
                        if (con) lista.Add(e.Archivo);
                        else lista.RemoveAll(a => string.Equals(a, e.Archivo, StringComparison.OrdinalIgnoreCase));
                    })) with { Detalle = con ? T("Avisa de sus cumpleaños") : T("No avisa de sus cumpleaños") };
                }).Prepend(Opcion.Linea).ToList();
                var interruptor = Opcion.Grupo(T("Avisarme de los cumpleaños"), si,
                    Ajuste(T("No"), Ui.Iconos.SinCalendarioTarta, !activos, true, () => Cambiar(() => pref.AvisosCumple = false)));
                return new[]
                {
                    interruptor,
                    Opcion.Grupo(T("A qué hora"), Horas.Select(h => Ajuste(new DateTime(2000, 1, 1, h, 0, 0).ToString("t", Cultura), null,
                        h == pref.HoraAvisos, activos, () => Cambiar(() => pref.HoraAvisos = h))).ToArray()),
                    Opcion.Grupo(T("Cuándo"),
                        Ajuste(T("Solo ese día"), null, pref.DiasAntesAvisos == 0, activos, () => Cambiar(() => pref.DiasAntesAvisos = 0)),
                        Ajuste(T("También el día antes"), null, pref.DiasAntesAvisos == 1, activos, () => Cambiar(() => pref.DiasAntesAvisos = 1)),
                        Ajuste(T("También una semana antes"), null, pref.DiasAntesAvisos == 7, activos, () => Cambiar(() => pref.DiasAntesAvisos = 7))),
                    Opcion.Grupo(T("Aniversarios de fallecimiento"),
                        Ajuste(T("Sí"), null, pref.AvisosAniversarios, activos, () => Cambiar(() => pref.AvisosAniversarios = true)),
                        Ajuste(T("No"), null, !pref.AvisosAniversarios, activos, () => Cambiar(() => pref.AvisosAniversarios = false))),
                    Opcion.Grupo(T("Aniversarios de boda"),
                        Ajuste(T("Sí"), null, pref.AvisosBodas, activos, () => Cambiar(() => pref.AvisosBodas = true)),
                        Ajuste(T("No"), null, !pref.AvisosBodas, activos, () => Cambiar(() => pref.AvisosBodas = false))),
                }.Concat(arboles).Concat(new[] { Opcion.Linea, proximos }).ToList();
            }
            var r = await p.Menu(T("Avisos de cumpleaños"), Todas(), o =>
            {
                if (o.AlElegir is not { } cambiar) return null;
                cambiar();
                return Todas();
            });
            if (r == null) return;
            if (r == si) await Activar();
            else if (r == proximos) await Proximos();
        }
    }

    /// <summary>Activa los avisos, pidiendo permiso si hace falta (y, si Android los tiene bloqueados, ofrece ir a sus ajustes).</summary>
    private static async Task Activar()
    {
        var pref = Preferencias.Actual;
        pref.AvisosCumple = true;
        pref.Guardar();
        Programar();
        if (App.Plataforma.AvisosPermitidos || await App.Plataforma.PedirPermisoAvisos()) return;
        if (await Principal.Actual.Preguntar(T("Los avisos están bloqueados"),
                T("Android no deja que Ramo muestre avisos. Actívalos en los ajustes del teléfono, en «Notificaciones»."),
                T("Abrir los ajustes"), T("Cancelar")) == 0)
            App.Plataforma.AbrirAjustesAvisos();
    }

    /// <summary>Los cumpleaños de los dos próximos meses (para ver qué se va a avisar).</summary>
    private static async Task Proximos()
    {
        var pref = Preferencias.Actual;
        var arboles = await Task.Run(() => Arboles().ToList());
        var hoy = DateTime.Today;
        var lineas = new List<string>();
        for (int d = 0; d < 61; d++)
        {
            var dia = hoy.AddDays(d);
            foreach (var f in Cumpleanos.Del(dia, arboles, pref.AvisosAniversarios, pref.AvisosBodas))
                lineas.Add(dia.ToString(T("d MMM"), Cultura) + " · " + f.Frase);
        }
        await Principal.Actual.Avisar(T("Próximos cumpleaños"), lineas.Count > 0 ? string.Join("\n", lineas)
            : T("No hay cumpleaños en los próximos dos meses. Para que salgan, pon el día y el mes de nacimiento en la ficha de cada persona."));
    }
}
