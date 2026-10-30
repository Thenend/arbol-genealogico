using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Io;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// La copia de seguridad, con su botón en «Mis árboles»: si los árboles están a salvo fuera del teléfono, guardar todos en
/// un archivo (la copia completa, ver <see cref="CopiaCompleta"/>), recuperarlos y el recordatorio. Lo demás se hace solo:
/// la copia de Google de Android y la de Documentos/Ramo.
/// </summary>
public static class CopiaSeguridad
{
    /// <summary>El color de «falta la copia»: un ámbar que se ve en todos los temas y no asusta como el rojo.</summary>
    public static readonly IBrush Ambar = new SolidColorBrush(Color.Parse("#C27A12"));

    /// <summary>Si la última copia completa tiene todo lo de ahora, y la frase que lo dice.</summary>
    public static (bool AlDia, string Texto) Estado()
    {
        var pref = Preferencias.Actual;
        if (pref.UltimaCopiaCompleta is not { } ultima) return (false, T("Aún no has guardado una copia fuera del teléfono"));
        return pref.CambiosSinCopiaDesde == null
            ? (true, T("Todo guardado. Última copia: {0}", Dia(ultima)))
            : (false, T("Hay cambios sin copia. Última copia: {0}", Dia(ultima)));
    }

    private static string Dia(DateTime f)
    {
        var hoy = DateTime.Today;
        if (f.Date == hoy) return T("hoy");
        if (f.Date == hoy.AddDays(-1)) return T("ayer");
        return f.ToString(f.Year == hoy.Year ? T("d MMM") : T("d MMM yyyy"), Cultura);
    }

    /// <summary>El escudo del estado: con una marca si está al día, con una exclamación si falta la copia.</summary>
    public static Control Escudo(bool alDia, double tam = 26) =>
        Ui.Icono(alDia ? Ui.Iconos.EscudoBien : Ui.Iconos.EscudoAviso, tam, alDia ? Ui.Verde : Ambar);

    // ---------- Recordatorio ----------

    /// <summary>
    /// La frase del recordatorio, que sale encima de los botones de «Mis árboles» (sin avisos del teléfono), o null si no
    /// toca: hay cambios que no están en ninguna copia desde hace un mes (la primera vez, desde hace tres días).
    /// </summary>
    public static string? Recordatorio()
    {
        var pref = Preferencias.Actual;
        // quien ya tenía árboles propios antes de que existiera la copia completa: como si los acabara de cambiar
        if (pref.CambiosSinCopiaDesde == null && pref.UltimaCopiaCompleta == null && App.Biblioteca.ArbolesPropios() > 0)
        {
            pref.CambiosSinCopiaDesde = DateTime.Now;
            pref.Guardar();
        }
        if (pref.CambiosSinCopiaDesde is not { } desde) return null;
        if (pref.UltimaCopiaCompleta == null)
            return DateTime.Now - desde >= TimeSpan.FromDays(3) ? T("Aún no has guardado una copia de tus árboles") : null;
        return DateTime.Now - desde >= TimeSpan.FromDays(30) ? T("Hace más de un mes que no guardas una copia de tus árboles") : null;
    }

    // ---------- La pantalla de la copia ----------

    /// <summary>
    /// El estado, en pocas palabras qué se guarda solo, y guardar la copia completa o recuperar. Después de guardar la copia
    /// se vuelve aquí, para ver que ya está.
    /// </summary>
    public static async Task Abrir(Control desde)
    {
        var p = Principal.Actual;
        while (true)
        {
            var guardar = new Opcion(T("Guardar una copia completa"), Ui.Iconos.Guardar, T("Todos tus árboles en un solo archivo, para Drive, el correo o un familiar"));
            var recuperar = new Opcion(T("Recuperar mis árboles"), Ui.Iconos.Carpeta, T("De una copia completa, al cambiar de teléfono o si se borró la aplicación"));
            var r = await p.Menu(T("Copia de seguridad"), new[] { Opcion.Recuadro("", Explicacion), guardar, recuperar });
            if (r == null) return;
            try
            {
                if (r == guardar) await GuardarTodo(desde);
                else if (r == recuperar) { await Recuperar(desde); return; }
            }
            catch (Exception ex) { await p.Error(T("No se pudo completar"), ex); return; }
        }
    }

    private static Control Explicacion()
    {
        var (alDia, texto) = Estado();
        var pila = new StackPanel { Spacing = 10, Margin = new Thickness(8, 4, 8, 2) };
        var fila = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var escudo = Escudo(alDia, 30);
        escudo.Margin = new Thickness(0, 0, 12, 0); escudo.VerticalAlignment = VerticalAlignment.Center;
        fila.Children.Add(escudo);
        var estado = new TextBlock { Text = texto, FontSize = Ui.L(16), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(estado, 1); fila.Children.Add(estado);
        pila.Children.Add(fila);
        pila.Children.Add(Ui.Parrafo(Biblioteca.CopiaActiva
            ? T("Tus árboles se guardan solos en este teléfono y en «{0}». Para no perderlos si se rompe o se pierde el teléfono, guarda de vez en cuando una copia completa fuera de él.", App.Plataforma.CarpetaCopia)
            : T("Tus árboles se guardan solos en este teléfono. Para no perderlos si se rompe o se pierde el teléfono, guarda de vez en cuando una copia completa fuera de él."), 14));
        return pila;
    }

    // ---------- Guardar la copia completa ----------

    /// <summary>Todos los árboles en un archivo, que se manda (al correo, a WhatsApp, a Drive) o se guarda en una carpeta.</summary>
    public static async Task GuardarTodo(Control desde)
    {
        var p = Principal.Actual;
        var (datos, n) = App.Biblioteca.CrearCopiaCompleta();
        if (n == 0)
        {
            await p.Avisar(T("No hay árboles que guardar"), T("Cuando tengas algún árbol, aquí podrás guardar una copia de todos."));
            return;
        }
        var nombre = T("Copia de mis árboles") + " " + DateTime.Now.ToString(T("d MMM yyyy"), Cultura);
        var mandar = new Opcion(T("Mandarla por correo, WhatsApp o Drive"), Ui.Iconos.Compartir, T("Lo más fácil: mándatela a ti o a un familiar"));
        var carpeta = new Opcion(T("Guardarla en una carpeta"), Ui.Iconos.Guardar, T("En Descargas, en Drive, en una tarjeta de memoria…"));
        var r = await p.Menu(N(n, "Copia completa: 1 árbol", "Copia completa: {0} árboles"), new[] { mandar, carpeta });
        if (r == null) return;
        if (r == mandar)
        {
            var ruta = App.Biblioteca.ArchivoParaCompartir(nombre, ArbolJson.Extension);
            await File.WriteAllBytesAsync(ruta, datos);
            // en el correo, quien la abra (o uno mismo, en el teléfono nuevo) sabe qué es y qué hacer con ella
            var texto = T("Copia de seguridad de mis árboles de Ramo. Para recuperarlos, abre este archivo con Ramo: {0}", Compartir.EnlacePlay);
            await App.Plataforma.Compartir(new[] { ruta }, Compartir.TipoArbol, T("Guardar la copia de tus árboles"), texto);
        }
        else
        {
            var almacen = TopLevel.GetTopLevel(desde)?.StorageProvider;
            if (almacen == null) return;
            // sin DefaultExtension: Avalonia en Android la añade al nombre propuesto aunque ya la lleve (ver Compartir.GuardarCopia)
            var archivo = await almacen.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = T("Guardar la copia de tus árboles"), SuggestedFileName = Biblioteca.Limpiar(nombre) + ArbolJson.Extension,
                FileTypeChoices = new[] { new FilePickerFileType(T("Árbol genealógico")) { Patterns = new[] { "*.ramo" }, MimeTypes = new[] { Compartir.TipoArbol } } },
            });
            if (archivo == null) return;
            await using (var s = await archivo.OpenWriteAsync()) await s.WriteAsync(datos);
            p.Aviso(T("Copia guardada"));
        }
        var pref = Preferencias.Actual;
        pref.UltimaCopiaCompleta = DateTime.Now;
        pref.CambiosSinCopiaDesde = null;
        pref.Guardar();
    }

    // ---------- Recuperar ----------

    /// <summary>
    /// Trae a «Mis árboles» los de una copia completa (o cualquier .ramo que se elija) o los de la copia de Documentos, tal
    /// cual: sin preguntar por fusiones y sin duplicar los que ya estén.
    /// </summary>
    public static async Task Recuperar(Control desde)
    {
        var p = Principal.Actual;
        var completa = new Opcion(T("De una copia completa"), Ui.Iconos.Documento, T("Elige el archivo en Drive o en Descargas. Si está en tu correo, tócalo allí"));
        var carpeta = App.Plataforma.PuedeLeerCopias
            ? new Opcion(T("De la carpeta «{0}»", App.Plataforma.CarpetaCopia), Ui.Iconos.Carpeta, T("Si se borró la aplicación o sus datos en este teléfono"))
            : null;
        var r = carpeta == null ? completa : await p.Menu(T("Recuperar mis árboles"), new[] { completa, carpeta });
        if (r == null) return;
        IReadOnlyList<ArchivoRecibido> archivos;
        if (r == carpeta)
        {
            // Android: el selector de carpetas se abre ya en la copia; con «Usar esta carpeta» se recuperan todos
            await p.Avisar(T("Recuperar mis árboles"),
                T("Se abrirá la carpeta «{0}». Pulsa «Usar esta carpeta» y después «Permitir»: se recuperarán todos los árboles que haya en ella.",
                  App.Plataforma.CarpetaCopia));
            var leidos = await App.Plataforma.LeerCopias();
            if (leidos == null) return;
            if (leidos.Count == 0)
            {
                await p.Avisar(T("No hay árboles en esa carpeta"),
                    T("Comprueba que es «{0}». Si guardaste copias en otro sitio, usa «Abrir archivo».", App.Plataforma.CarpetaCopia));
                return;
            }
            archivos = leidos;
        }
        else
        {
            var almacen = TopLevel.GetTopLevel(desde)?.StorageProvider;
            if (almacen == null) return;
            var elegidos = await almacen.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = T("Recuperar árboles"), AllowMultiple = true, FileTypeFilter = new[] { FilePickerFileTypes.All },
            });
            var lista = new List<ArchivoRecibido>();
            foreach (var f in elegidos)
            {
                await using var st = await f.OpenReadAsync();
                using var ms = new MemoryStream();
                await st.CopyToAsync(ms);
                lista.Add(new ArchivoRecibido(f.Name, ms.ToArray()));
            }
            archivos = lista;
        }
        await Importar(archivos);
    }

    /// <summary>Ha llegado una copia completa (tocada en el correo, en Drive…): se ofrece recuperar sus árboles.</summary>
    public static async Task Recibir(ArchivoRecibido r)
    {
        var p = Principal.Actual;
        CopiaCompleta.Leida copia;
        try { copia = CopiaCompleta.Leer(r.Datos); }
        catch
        {
            await p.Avisar(T("No se puede abrir «{0}»", r.Nombre), T("La copia está dañada o incompleta."));
            return;
        }
        var texto = T("Se añaden a «Mis árboles» los que no tengas ya. No se borra ni se cambia nada de lo que tienes.");
        if (copia.Fecha is { } f) texto += "\n\n" + T("Fecha de la copia: {0}.", Dia(f));
        if (await p.Preguntar(N(copia.Arboles.Count, "Copia con 1 árbol", "Copia con {0} árboles"), texto, T("Recuperar"), T("Cancelar")) != 0) return;
        await Importar(new[] { r });
    }

    /// <summary>Guarda en «Mis árboles» los árboles de esos archivos (los de las copias completas, uno a uno) y lo cuenta.</summary>
    private static async Task Importar(IReadOnlyList<ArchivoRecibido> archivos)
    {
        var p = Principal.Actual;
        int antes = App.Biblioteca.Listar().Count, leidos = 0;
        var fallos = new List<string>();
        void Uno(string nombre, string texto)
        {
            try { App.Biblioteca.Importar(ArbolJson.Deserializar(texto.TrimStart('﻿'), out _), nombre); leidos++; }
            catch { fallos.Add(nombre); }
        }
        foreach (var f in archivos)
        {
            if (!CopiaCompleta.Es(f.Datos)) { Uno(f.Nombre, new System.Text.UTF8Encoding(false).GetString(f.Datos)); continue; }
            try
            {
                var copia = CopiaCompleta.Leer(f.Datos);
                foreach (var (archivo, contenido) in copia.Arboles) Uno(archivo, contenido);
                // los árboles ya están en esa copia, fuera del teléfono (p. ej. al estrenar uno): es la última que hay
                var pref = Preferencias.Actual;
                if (copia.Fecha is { } fecha && (pref.UltimaCopiaCompleta == null || fecha > pref.UltimaCopiaCompleta))
                {
                    pref.UltimaCopiaCompleta = fecha;
                    pref.Guardar();
                }
            }
            catch { fallos.Add(f.Nombre); }
        }
        int nuevos = App.Biblioteca.Listar().Count - antes;
        if (p.Pantalla is PantallaInicio inicio) inicio.Recargar();
        else p.Ir(new PantallaInicio());
        if (fallos.Count > 0)
            await p.Avisar(T("Algunos archivos no se pudieron recuperar"), T("No son árboles genealógicos o están dañados: {0}", string.Join(", ", fallos)));
        else if (nuevos > 0)
            p.Aviso(N(nuevos, "Árbol recuperado", "{0} árboles recuperados"));
        else if (leidos > 0)
            p.Aviso(T("Ya tenías todos esos árboles"));
    }
}
