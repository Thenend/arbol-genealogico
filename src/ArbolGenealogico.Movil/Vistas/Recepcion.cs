using System.Text;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// Lo que pasa cuando llega un árbol de fuera: se comprueba que lo sea y se ofrece guardarlo como árbol nuevo o fusionarlo
/// con uno que ya esté en el teléfono (si comparten personas, se propone ese).
/// </summary>
public static class Recepcion
{
    public static async Task Atender(ArchivoRecibido r)
    {
        var p = Principal.Actual;
        Arbol a;
        // un GEDCOM (de otro programa de genealogía) se convierte; lo que no es dato de este programa va a la historia
        bool gedcom = Gedcom.EsGedcom(r.Nombre) || Gedcom.PareceGedcom(r.Datos);
        var avisos = new List<string>();
        try
        {
            if (gedcom) a = Gedcom.Importar(r.Datos, r.Nombre, out avisos);
            else
            {
                var texto = new UTF8Encoding(false).GetString(r.Datos).TrimStart('\ufeff');
                a = ArbolJson.Deserializar(texto, out _);
            }
        }
        catch (Exception ex)
        {
            await p.Avisar(T("No se puede abrir «{0}»", r.Nombre), gedcom
                ? ex is InvalidDataException ? ex.Message : T("Parece un archivo GEDCOM, pero no se ha podido leer.")
                : T("No es un árbol genealógico (.ramo ni GEDCOM) o el archivo está dañado."));
            return;
        }
        if (gedcom)
            await p.Avisar(T("Árbol importado de GEDCOM"), T("{0}. Si traía fechas, lugares, oficios o notas, están en la ficha de cada persona, en «Su historia».",
                    Principal.Personas(a.Personas.Count)) +
                (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos.Select(x => "• " + x)) : ""));
        var nombre = string.IsNullOrWhiteSpace(a.Nombre) ? Path.GetFileNameWithoutExtension(r.Nombre) : a.Nombre;
        if (string.IsNullOrWhiteSpace(a.Nombre)) a.Nombre = nombre;
        var archivo = string.IsNullOrWhiteSpace(r.Nombre) || !Principal.EsArbol(r.Nombre) ? nombre + ArbolJson.Extension : r.Nombre;

        var relacionados = App.Biblioteca.Relacionados(a);
        var opciones = new List<Opcion>();
        var abrir = new Opcion(T("Abrirlo"), Ui.Iconos.Carpeta, T("Se guarda en «Mis árboles» como un árbol más"));
        opciones.Add(abrir);
        var fusiones = new Dictionary<Opcion, EntradaArbol>();
        var yaLoTiene = new Dictionary<Opcion, EntradaArbol>();
        foreach (var (e, comunes) in relacionados.Take(3))
        {
            // si ese árbol ya tiene todo lo que trae (p. ej. es una copia del suyo), no se ofrece fusionar sino abrirlo
            bool aporta = true;
            try { aporta = Fusion.Probar(App.Biblioteca.Cargar(e.Ruta, out _), a).AportaAlgo; } catch { }
            if (!aporta)
            {
                var abrirEse = new Opcion(T("Abrir «{0}»", e.Nombre), Ui.Iconos.Carpeta, T("Ya tienes todo lo que trae este archivo"));
                yaLoTiene[abrirEse] = e;
                opciones.Insert(0, abrirEse);
                continue;
            }
            var o = new Opcion(T("Fusionarlo con «{0}»", e.Nombre), Ui.Iconos.Fusionar,
                N(comunes, "Tenéis 1 persona en común: se añade lo que te falte", "Tenéis {0} personas en común: se añade lo que te falte"));
            fusiones[o] = e;
            opciones.Add(o);
        }
        if (yaLoTiene.Count > 0)
        {
            // ya lo tiene: abrir el suyo va primero, y guardar otra copia queda como opción aparte
            int i = opciones.IndexOf(abrir);
            abrir = new Opcion(T("Guardarlo también como árbol aparte"), Ui.Iconos.Nuevo, T("Una copia más en «Mis árboles»"));
            opciones[i] = abrir;
        }
        var otro = new Opcion(T("Fusionarlo con otro árbol…"), Ui.Iconos.Fusionar, T("Eliges quién es quién"));
        if (App.Biblioteca.Listar().Count > relacionados.Count) opciones.Add(otro);

        var eleccion = await p.Menu(T("Has recibido «{0}» ({1})", nombre, Principal.Personas(a.Personas.Count)), opciones);
        if (eleccion == null) return;
        try
        {
            if (eleccion == abrir)
            {
                int antes = App.Biblioteca.Listar().Count;
                var ruta = App.Biblioteca.Importar(a, archivo);
                if (App.Biblioteca.Listar().Count == antes) p.Aviso(T("Ya tenías este árbol tal cual: se abre el que tenías"));
                PantallaInicio.Abrir(ruta);
            }
            else if (yaLoTiene.TryGetValue(eleccion, out var suyo)) PantallaInicio.Abrir(suyo.Ruta);
            else if (fusiones.TryGetValue(eleccion, out var destino)) await PantallaFusion.Fusionar(destino.Ruta, a, nombre);
            else if (eleccion == otro)
            {
                var lista = App.Biblioteca.Listar();
                var ops = lista.Select(e => new Opcion(e.Nombre, Ui.Iconos.Fusionar, Principal.Personas(e.Personas))).ToList();
                var elegido = await p.Menu(T("¿Con qué árbol lo fusionas?"), ops);
                if (elegido != null) await PantallaFusion.Fusionar(lista[ops.IndexOf(elegido)].Ruta, a, nombre);
            }
        }
        catch (Exception ex) { await p.Error(T("No se pudo completar"), ex); }
    }
}
