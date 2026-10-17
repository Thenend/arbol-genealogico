using System.Text;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;

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
        try
        {
            var texto = new UTF8Encoding(false).GetString(r.Datos).TrimStart('﻿');
            a = ArbolJson.Deserializar(texto, out _);
        }
        catch (Exception)
        {
            await p.Avisar("No se puede abrir «" + r.Nombre + "»", "No es un árbol genealógico (.tree) o el archivo está dañado.");
            return;
        }
        var nombre = string.IsNullOrWhiteSpace(a.Nombre) ? Path.GetFileNameWithoutExtension(r.Nombre) : a.Nombre;
        if (string.IsNullOrWhiteSpace(a.Nombre)) a.Nombre = nombre;
        var archivo = string.IsNullOrWhiteSpace(r.Nombre) || !Principal.EsArbol(r.Nombre) ? nombre + ArbolJson.Extension : r.Nombre;

        var relacionados = App.Biblioteca.Relacionados(a);
        var opciones = new List<Opcion>();
        var abrir = new Opcion("Abrirlo", Ui.Iconos.Carpeta, "Se guarda en «Mis árboles» como un árbol más");
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
                var abrirEse = new Opcion("Abrir «" + e.Nombre + "»", Ui.Iconos.Carpeta, "Ya tienes todo lo que trae este archivo");
                yaLoTiene[abrirEse] = e;
                opciones.Insert(0, abrirEse);
                continue;
            }
            var o = new Opcion("Fusionarlo con «" + e.Nombre + "»", Ui.Iconos.Fusionar,
                $"Tenéis {Principal.Personas(comunes)} en común: se añade lo que te falte");
            fusiones[o] = e;
            opciones.Add(o);
        }
        if (yaLoTiene.Count > 0)
        {
            // ya lo tiene: abrir el suyo va primero, y guardar otra copia queda como opción aparte
            int i = opciones.IndexOf(abrir);
            abrir = new Opcion("Guardarlo también como árbol aparte", Ui.Iconos.Nuevo, "Una copia más en «Mis árboles»");
            opciones[i] = abrir;
        }
        var otro = new Opcion("Fusionarlo con otro árbol…", Ui.Iconos.Fusionar, "Eliges quién es quién");
        if (App.Biblioteca.Listar().Count > relacionados.Count) opciones.Add(otro);

        var eleccion = await p.Menu($"Has recibido «{nombre}» ({Principal.Personas(a.Personas.Count)})", opciones);
        if (eleccion == null) return;
        try
        {
            if (eleccion == abrir)
            {
                int antes = App.Biblioteca.Listar().Count;
                var ruta = App.Biblioteca.Importar(a, archivo);
                if (App.Biblioteca.Listar().Count == antes) p.Aviso("Ya tenías este árbol tal cual: se abre el que tenías");
                PantallaInicio.Abrir(ruta);
            }
            else if (yaLoTiene.TryGetValue(eleccion, out var suyo)) PantallaInicio.Abrir(suyo.Ruta);
            else if (fusiones.TryGetValue(eleccion, out var destino)) await PantallaFusion.Fusionar(destino.Ruta, a, nombre);
            else if (eleccion == otro)
            {
                var lista = App.Biblioteca.Listar();
                var ops = lista.Select(e => new Opcion(e.Nombre, Ui.Iconos.Fusionar, Principal.Personas(e.Personas))).ToList();
                var elegido = await p.Menu("¿Con qué árbol lo fusionas?", ops);
                if (elegido != null) await PantallaFusion.Fusionar(lista[ops.IndexOf(elegido)].Ruta, a, nombre);
            }
        }
        catch (Exception ex) { await p.Error("No se pudo completar", ex); }
    }
}
