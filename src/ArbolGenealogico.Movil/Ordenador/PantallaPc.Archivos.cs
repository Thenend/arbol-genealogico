using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Movil.Vistas;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Ordenador;

/// <summary>Los archivos: nuevo, abrir, guardar, los árboles enlazados y, al cerrar, los cambios sin guardar.</summary>
public sealed partial class PantallaPc
{
    private static readonly FilePickerFileType TipoRamo = new(T("Árbol genealógico")) { Patterns = new[] { "*.ramo" } };

    private IStorageProvider? Almacen => TopLevel.GetTopLevel(this)?.StorageProvider;

    /// <summary>Al abrir la aplicación: el último árbol, o la bienvenida.</summary>
    private void Empezar()
    {
        if (Doc != null) return;
        if (Preferencias.Actual.UltimoArchivo is { } ultimo && File.Exists(ultimo) && Cargar(ultimo, silencioso: true)) { CumpleanosDeHoy(); return; }
        MostrarBienvenida();
    }

    /// <summary>
    /// Al abrir la aplicación, los cumpleaños de hoy del árbol abierto, en un aviso abajo (en el ordenador no hay avisos
    /// con la aplicación cerrada, como en el teléfono).
    /// </summary>
    private void CumpleanosDeHoy()
    {
        var pref = Preferencias.Actual;
        if (!pref.AvisosCumple || Doc is not { } d) return;
        var fiestas = Core.Presentacion.Cumpleanos.Del(DateTime.Today, new[] { (d.Ruta ?? "", d.Arbol) }, pref.AvisosAniversarios, pref.AvisosBodas);
        if (fiestas.Count > 0)
            Principal.Actual.Aviso(Core.Presentacion.Cumpleanos.Cuando(0) + "\n" + string.Join("\n", fiestas.Select(f => f.Frase)), 8);
    }

    private void MostrarBienvenida()
    {
        var pila = new StackPanel { Spacing = 14, Width = 380 };
        var icono = new Image { Width = 88, Height = 88, HorizontalAlignment = HorizontalAlignment.Center };
        try { icono.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/icono.png"))); }
        catch { }
        pila.Children.Add(icono);
        var titulo = Ui.Titulo(T("Tu árbol genealógico"), 24);
        titulo.HorizontalAlignment = HorizontalAlignment.Center; titulo.TextAlignment = TextAlignment.Center;
        pila.Children.Add(titulo);
        var texto = Ui.Parrafo(T("Empieza por ti o por quien quieras: después se añaden sus padres, hermanos, parejas e hijos."));
        texto.TextAlignment = TextAlignment.Center;
        pila.Children.Add(texto);
        Button Ancho(Button b) { b.HorizontalAlignment = HorizontalAlignment.Stretch; return b; }
        pila.Children.Add(Ancho(Ui.Boton(T("Nuevo árbol"), () => _ = Nuevo(), principal: true, icono: Ui.Iconos.Nuevo)));
        pila.Children.Add(Ancho(Ui.Boton(T("Abrir un árbol…"), () => _ = Abrir(), icono: Ui.Iconos.Carpeta)));
        pila.Children.Add(Ancho(Ui.Boton(T("Ver un árbol de ejemplo"), () => _ = AbrirEjemplo(), icono: Ui.Iconos.Galeria)));
        _bienvenida.Child = new Border
        {
            Child = pila, Background = Ui.Panel, CornerRadius = new CornerRadius(18), Padding = new Thickness(32, 28),
            BorderBrush = Ui.Linea, BorderThickness = new Thickness(1),
        };
        _bienvenida.IsVisible = true;
        ActualizarBarra();
    }

    /// <summary>Pone un documento como el único abierto (o encima de los demás, si viene de un enlace).</summary>
    private void Mostrar(Documento d, bool encima, bool encuadrar = true)
    {
        if (!encima) _pila.Clear();
        if (Doc is { } anterior) anterior.Seleccion = _lienzo.Seleccion;
        _pila.Add(d);
        Pintar(encuadrar);
        if (_pila.Count == 1 && d.Ruta != null) { Preferencias.Actual.UltimoArchivo = d.Ruta; Preferencias.Actual.Guardar(); }
    }

    private void Pintar(bool encuadrar)
    {
        var d = Doc;
        _bienvenida.IsVisible = d == null;
        _controlesVista.IsVisible = d != null;
        _lienzo.IsVisible = d != null;
        if (d == null) { MostrarBienvenida(); return; }
        _lienzo.Seleccion = null;
        _lienzo.Mostrar(d.Arbol, encuadrar);
        if (d.Seleccion != null && d.Arbol.Buscar(d.Seleccion) != null)
        {
            _lienzo.Seleccion = d.Seleccion;
            if (!encuadrar) _lienzo.CentrarEn(d.Seleccion, Math.Max(_lienzo.Escala, 0.6), false);
        }
        _buscar.Text = "";
        ActualizarFicha();
        ActualizarBarra();
        _lienzo.Focus();
    }

    /// <summary>Abre un archivo (.ramo, o un GEDCOM de otro programa, que se abre sin guardar). False si no se pudo.</summary>
    private bool Cargar(string ruta, bool silencioso = false, bool encima = false)
    {
        try { return AbrirDatos(Path.GetFileName(ruta), File.ReadAllBytes(ruta), ruta, silencioso, encima); }
        catch (Exception ex)
        {
            if (!silencioso) _ = Principal.Actual.Error(T("No se pudo abrir el árbol"), ex);
            return false;
        }
    }

    private bool AbrirDatos(string nombre, byte[] datos, string? ruta, bool silencioso, bool encima)
    {
        bool gedcom = Gedcom.EsGedcom(nombre) || Gedcom.PareceGedcom(datos);
        List<string> avisos;
        Arbol a;
        try
        {
            a = gedcom ? Gedcom.Importar(datos, nombre, out avisos)
                : ArbolJson.Deserializar(new System.Text.UTF8Encoding(false).GetString(datos).TrimStart('﻿'), out avisos);
            if (a.Personas.Count == 0) throw new InvalidDataException();
        }
        catch (Exception ex)
        {
            if (!silencioso)
                _ = Principal.Actual.Avisar(T("No se puede abrir «{0}»", nombre), gedcom
                    ? ex is InvalidDataException && ex.Message.Length > 0 ? ex.Message : T("Parece un archivo GEDCOM, pero no se ha podido leer.")
                    : T("No es un árbol genealógico (.ramo ni GEDCOM) o el archivo está dañado."));
            return false;
        }
        if (string.IsNullOrWhiteSpace(a.Nombre)) a.Nombre = Path.GetFileNameWithoutExtension(nombre);
        // un GEDCOM se abre como árbol nuevo sin guardar (se guarda como .ramo); un .tree o .json antiguo, en su archivo
        var d = new Documento(a, gedcom ? null : ruta) { Modificado = gedcom, Propio = gedcom };
        Mostrar(d, encima);
        if (gedcom)
            _ = Principal.Actual.Avisar(T("Árbol importado de GEDCOM"), T("{0}. Si traía fechas, lugares, oficios o notas, están en la ficha de cada persona, en «Su historia».",
                    Principal.Personas(a.Personas.Count)) + "\n\n" + T("Todavía no está guardado: al guardarlo se guarda como árbol (.ramo).") +
                (avisos.Count > 0 ? "\n\n" + string.Join("\n", avisos.Distinct().Select(x => "• " + x)) : ""));
        else if (avisos.Count > 0 && !silencioso)
            Principal.Actual.Aviso(string.Join("\n", avisos.Distinct()), 5);
        return true;
    }

    /// <summary>
    /// Un archivo que llega de fuera (doble clic en el Explorador, arrastrado a la ventana): si comparte personas con el
    /// árbol abierto y trae algo nuevo, se ofrece fusionarlo con él; si no, se abre.
    /// </summary>
    internal async Task AbrirRecibido(ArchivoRecibido r)
    {
        if (Doc is { } d && !Gedcom.EsGedcom(r.Nombre) && Principal.EsArbol(r.Nombre))
        {
            Arbol? otro = null;
            try { otro = ArbolJson.Deserializar(new System.Text.UTF8Encoding(false).GetString(r.Datos).TrimStart('﻿'), out _); }
            catch { }
            bool mismo = r.Ruta != null && string.Equals(r.Ruta, d.Ruta, StringComparison.OrdinalIgnoreCase);
            if (otro != null && !mismo && otro.Personas.Any(p => d.Arbol.Buscar(p.Id) != null) && Fusion.Probar(d.Arbol, otro).AportaAlgo)
            {
                var nombre = string.IsNullOrWhiteSpace(otro.Nombre) ? Path.GetFileNameWithoutExtension(r.Nombre) : otro.Nombre;
                var abrir = new Opcion(T("Abrirlo"), Ui.Iconos.Carpeta, T("En lugar de «{0}»", d.Titulo));
                var fusionar = new Opcion(T("Fusionarlo con «{0}»", d.Titulo), Ui.Iconos.Fusionar, T("Se añade lo que le falte a «{0}»", d.Titulo));
                var e = await Principal.Actual.Menu(T("«{0}» tiene personas de «{1}»", nombre, d.Titulo), new[] { abrir, fusionar });
                if (e == null) return;
                if (e == fusionar) { await Fusionar(otro, nombre); return; }
            }
        }
        if (!await PuedeCerrar()) return;
        AbrirDatos(r.Nombre, r.Datos, r.Ruta, false, false);
    }

    private async Task Nuevo()
    {
        if (!Licencia.PuedeCrearArbol && !await PantallaCompleta.PorArboles()) return;
        if (!await PuedeCerrar()) return;
        var nombre = await Principal.Actual.PedirTexto(T("Nombre"), "", T("Empezar"), T("Persona principal del árbol"));
        if (nombre == null) return;
        var a = Arbol.Nuevo(string.IsNullOrWhiteSpace(nombre) ? T("Mi familia") : T("Familia de {0}", nombre), nombre);
        Mostrar(new Documento(a, null) { Propio = true, Modificado = true }, false);
        _lienzo.Seleccion = a.RaizId;
        await Editar(a.RaizId, false);
    }

    private async Task Abrir()
    {
        if (Almacen is not { } almacen) return;
        var archivos = await almacen.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = T("Abrir árbol"),
            FileTypeFilter = new[]
            {
                new FilePickerFileType(T("Árbol genealógico")) { Patterns = new[] { "*.ramo", "*.ged", "*.gedcom", "*.tree", "*.json" } },
                FilePickerFileTypes.All,
            },
            SuggestedStartLocation = Doc?.Ruta is { } r ? await almacen.TryGetFolderFromPathAsync(Path.GetDirectoryName(r)!) : null,
        });
        if (archivos.Count == 0 || archivos[0].TryGetLocalPath() is not { } ruta) return;
        if (!await PuedeCerrar()) return;
        Cargar(ruta);
    }

    private async Task AbrirEjemplo()
    {
        using var s = AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/Ejemplos/familia-garcia.ramo"));
        using var lector = new StreamReader(s);
        var a = PantallaInicio.TraducirEjemplo(ArbolJson.Deserializar(await lector.ReadToEndAsync(), out _));
        Mostrar(new Documento(a, null), false);
    }

    /// <summary>
    /// Guarda el árbol en su archivo (o pregunta dónde, la primera vez o con «Guardar como»). False si no se ha guardado.
    /// </summary>
    private async Task<bool> Guardar(Documento? d, bool como)
    {
        if (d == null) return false;
        var ruta = d.Ruta;
        // en la versión gratuita, un árbol propio más (nuevo o de GEDCOM) solo si no hay ya otro guardado
        if (d.Propio && ruta == null && !Licencia.Completa && Preferencias.Actual.PropiosPc.Count(File.Exists) >= Licencia.ArbolesGratis
            && !await PantallaCompleta.PorArboles()) return false;
        if (como || ruta == null)
        {
            if (Almacen is not { } almacen) return false;
            var archivo = await almacen.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = T("Guardar árbol"), SuggestedFileName = Biblioteca.Limpiar(d.Titulo) + ArbolJson.Extension, DefaultExtension = "ramo",
                FileTypeChoices = new[] { TipoRamo }, ShowOverwritePrompt = true,
                SuggestedStartLocation = (d.Ruta ?? Preferencias.Actual.UltimoArchivo) is { } r && Path.GetDirectoryName(r) is { } carpeta
                    ? await almacen.TryGetFolderFromPathAsync(carpeta) : await almacen.TryGetWellKnownFolderAsync(WellKnownFolder.Documents),
            });
            if (archivo?.TryGetLocalPath() is not { } nueva) return false;
            // los enlaces a otros árboles van relativos a la carpeta del archivo: si cambia de carpeta, se rehacen
            if (d.Ruta != null && !string.Equals(Path.GetDirectoryName(d.Ruta), Path.GetDirectoryName(nueva), StringComparison.OrdinalIgnoreCase))
                RebasarEnlaces(d.Arbol, d.Ruta, nueva);
            ruta = nueva;
        }
        try
        {
            d.Arbol.Nombre = Path.GetFileNameWithoutExtension(ruta);
            ArbolJson.Guardar(ruta, d.Arbol);
            d.Ruta = ruta;
            d.Modificado = false;
            if (d.Propio) AnotarPropio(ruta);
            if (_pila.Count > 0 && ReferenceEquals(_pila[0], d)) { Preferencias.Actual.UltimoArchivo = ruta; Preferencias.Actual.Guardar(); }
            ActualizarBarra();
            Principal.Actual.Aviso(T("Guardado"), 1.5);
            return true;
        }
        catch (Exception ex)
        {
            await Principal.Actual.Error(T("No se pudo guardar"), ex);
            return false;
        }
    }

    private static void RebasarEnlaces(Arbol a, string rutaVieja, string rutaNueva)
    {
        foreach (var p in a.Personas.Where(p => !string.IsNullOrEmpty(p.ArbolEnlazado) && !Path.IsPathRooted(p.ArbolEnlazado)))
            p.ArbolEnlazado = ArbolJson.RutaRelativa(rutaNueva, ArbolJson.ResolverRuta(rutaVieja, p.ArbolEnlazado!));
    }

    /// <summary>
    /// Antes de cerrar un árbol con cambios, pregunta si guardarlos. False: no se cierra (se ha cancelado o no se ha podido
    /// guardar).
    /// </summary>
    private async Task<bool> PreguntarGuardar(Documento d)
    {
        if (!d.Modificado) return true;
        int r = await Principal.Actual.Preguntar(T("¿Guardar los cambios de «{0}»?", d.Titulo), d.Ruta == null ? T("Todavía no está guardado.") : "",
            T("Guardar"), T("No guardar"), T("Cancelar"));
        if (r == 0) return await Guardar(d, false);
        return r == 1;
    }

    /// <summary>Si se pueden cerrar todos los árboles abiertos (los que tienen cambios preguntan antes).</summary>
    internal async Task<bool> PuedeCerrar()
    {
        foreach (var d in _pila.Where(x => x.Modificado).Reverse().ToList())
            if (!await PreguntarGuardar(d)) return false;
        return true;
    }

    /// <summary>Vuelve al árbol desde el que se siguió el enlace.</summary>
    private async Task Atras(bool preguntar)
    {
        if (_pila.Count < 2 || Doc is not { } d) return;
        if (preguntar && !await PreguntarGuardar(d)) return;
        _pila.RemoveAt(_pila.Count - 1);
        Pintar(false);
    }

    // ---------- Árboles enlazados ----------

    private async Task AbrirEnlace(string id)
    {
        if (Doc is not { } d || d.Arbol.Buscar(id) is not { ArbolEnlazado: { Length: > 0 } enlace } p) return;
        if (d.Ruta == null)
        {
            await Principal.Actual.Avisar(T("Guarda este árbol primero"), T("Los árboles enlazados se buscan junto al archivo de este árbol: guárdalo y vuelve a probar."));
            return;
        }
        var ruta = ArbolJson.ResolverRuta(d.Ruta, enlace);
        if (!File.Exists(ruta))
        {
            int r = await Principal.Actual.Preguntar(T("No se encuentra el árbol"),
                T("La tarjeta de {0} enlaza con «{1}», que no está en la carpeta de este árbol. ¿Lo buscas?", Core.Presentacion.Nombres.Mostrar(p), Path.GetFileNameWithoutExtension(enlace)),
                T("Buscar…"), T("Cancelar"));
            if (r != 0 || await ElegirArchivoArbol(T("El árbol de {0}", Core.Presentacion.Nombres.Mostrar(p))) is not { } elegido) return;
            Cambiar(a => a.Obtener(id).ArbolEnlazado = ArbolJson.RutaRelativa(d.Ruta, elegido));
            ruta = elegido;
        }
        // si ya está abierto más atrás, se vuelve a él
        int ya = _pila.FindIndex(x => x.Ruta != null && string.Equals(x.Ruta, ruta, StringComparison.OrdinalIgnoreCase));
        if (ya >= 0)
        {
            foreach (var x in _pila.Skip(ya + 1).Reverse().ToList())
                if (!await PreguntarGuardar(x)) return;
            while (_pila.Count - 1 > ya) _pila.RemoveAt(_pila.Count - 1);
            Pintar(false);
            return;
        }
        if (Cargar(ruta, encima: true)) Principal.Actual.Aviso(T("«Volver» (Alt+←) vuelve al árbol anterior"), 2.5);
    }

    private async Task<string?> ElegirArchivoArbol(string titulo)
    {
        if (Almacen is not { } almacen) return null;
        var archivos = await almacen.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = titulo, FileTypeFilter = new[] { TipoRamo, FilePickerFileTypes.All },
            SuggestedStartLocation = Doc?.Ruta is { } r ? await almacen.TryGetFolderFromPathAsync(Path.GetDirectoryName(r)!) : null,
        });
        return archivos.Count > 0 ? archivos[0].TryGetLocalPath() : null;
    }

    /// <summary>En la ficha, «Elegir» su árbol enlazado: un archivo (o ninguno), relativo a la carpeta de este árbol.</summary>
    private async Task<(bool, string?)> ElegirEnlace(Persona p)
    {
        if (Doc is not { } d) return (false, null);
        var elegir = new Opcion(T("Elegir un archivo…"), Ui.Iconos.Carpeta);
        var quitar = new Opcion(T("Sin árbol enlazado"), Ui.Iconos.Cerrar);
        var ops = string.IsNullOrEmpty(p.ArbolEnlazado) ? new[] { elegir } : new[] { elegir, quitar };
        var r = ops.Length == 1 ? elegir : await Principal.Actual.Menu(T("El árbol de {0}", Core.Presentacion.Nombres.Mostrar(p)), ops);
        if (r == null) return (false, null);
        if (r == quitar) return (true, null);
        if (d.Ruta == null && !await GuardarAntes()) return (false, null);
        if (await ElegirArchivoArbol(T("El árbol de {0}", Core.Presentacion.Nombres.Mostrar(p))) is not { } ruta) return (false, null);
        return (true, ArbolJson.RutaRelativa(d.Ruta!, ruta));
    }

    /// <summary>Para enlazar con otro árbol hace falta que este esté guardado (el enlace es relativo a su carpeta).</summary>
    private async Task<bool> GuardarAntes()
    {
        if (Doc is not { } d) return false;
        if (d.Ruta != null && !d.Modificado) return true;
        int r = await Principal.Actual.Preguntar(T("Guarda este árbol primero"), T("Para enlazarlo con otro árbol, este tiene que estar guardado."), T("Guardar"), T("Cancelar"));
        return r == 0 && await Guardar(d, false);
    }

    /// <summary>
    /// El árbol propio de una persona: se guarda con su familia de sangre y las parejas de todos ellos, su tarjeta enlaza
    /// con él y se abre («Volver» regresa a este).
    /// </summary>
    private async Task CrearArbolPropio(string id)
    {
        if (Doc is not { } d || d.Arbol.Buscar(id) is not { } p || id == d.Arbol.RaizId) return;
        if (!await GuardarAntes() || d.Ruta == null || Almacen is not { } almacen) return;
        var titulo = T("Familia de {0}", Core.Presentacion.Nombres.Mostrar(p));
        var archivo = await almacen.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = T("¿Crear el árbol de {0}?", Core.Presentacion.Nombres.Mostrar(p)), SuggestedFileName = Biblioteca.Limpiar(titulo) + ArbolJson.Extension,
            DefaultExtension = "ramo", FileTypeChoices = new[] { TipoRamo }, ShowOverwritePrompt = true,
            SuggestedStartLocation = await almacen.TryGetFolderFromPathAsync(Path.GetDirectoryName(d.Ruta)!),
        });
        if (archivo?.TryGetLocalPath() is not { } ruta) return;
        if (string.Equals(ruta, d.Ruta, StringComparison.OrdinalIgnoreCase))
        {
            await Principal.Actual.Avisar(T("Elige otro archivo"), T("El árbol nuevo no puede ocupar el sitio de este."));
            return;
        }
        var nuevo = d.Arbol.ExtraerFamiliaDe(id);
        nuevo.Nombre = Path.GetFileNameWithoutExtension(ruta);
        RebasarEnlaces(nuevo, d.Ruta, ruta);
        if (nuevo.Buscar(d.Arbol.RaizId) is { } dueno && dueno.Id != nuevo.RaizId && string.IsNullOrEmpty(dueno.ArbolEnlazado))
            dueno.ArbolEnlazado = ArbolJson.RutaRelativa(ruta, d.Ruta);
        try { ArbolJson.Guardar(ruta, nuevo); }
        catch (Exception ex) { await Principal.Actual.Error(T("No se pudo guardar"), ex); return; }
        Cambiar(a => a.Obtener(id).ArbolEnlazado = ArbolJson.RutaRelativa(d.Ruta, ruta));
        if (!await Guardar(d, false)) return;
        if (Cargar(ruta, encima: true)) Principal.Actual.Aviso(T("«Volver» (Alt+←) vuelve al árbol anterior"), 2.5);
    }

    // ---------- Versión gratuita: los árboles propios ----------

    private int ContarPropios() =>
        Preferencias.Actual.PropiosPc.Count(File.Exists) + _pila.Count(x => x.Propio && x.Ruta == null);

    private static void AnotarPropio(string ruta)
    {
        var lista = Preferencias.Actual.PropiosPc;
        if (lista.Contains(ruta, StringComparer.OrdinalIgnoreCase)) return;
        lista.Add(ruta);
        Preferencias.Actual.Guardar();
    }
}
