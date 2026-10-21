using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>«Mis árboles»: los árboles guardados en el teléfono, para abrirlos, compartirlos o crear uno nuevo.</summary>
public sealed class PantallaInicio : DockPanel, IPantalla
{
    /// <summary>Lo que se ve no pasa de este ancho (en una tableta apaisada, la lista y los botones van centrados).</summary>
    private const double AnchoMaximo = 760;
    private readonly StackPanel _lista = new() { Spacing = 10, Margin = new Thickness(16, 8, 16, 24), MaxWidth = AnchoMaximo };
    /// <summary>La estrella de «Ramo completo» (solo en la versión gratuita: para verlo, comprarlo o restaurar la compra).</summary>
    private readonly Button _completa;

    public PantallaInicio()
    {
        var cabecera = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Margin = new Thickness(16, 14, 8, 6),
        };
        var icono = new Image { Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/icono.png"))), Width = 40 * Ui.EscalaIcono, Height = 40 * Ui.EscalaIcono, Margin = new Thickness(0, 0, 12, 0) };
        cabecera.Children.Add(icono);
        var titulo = Ui.Titulo(T("Mis árboles"), 24);
        titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 1); cabecera.Children.Add(titulo);
        _completa = Ui.BotonIcono(Ui.Iconos.Estrella, T("Ramo completo"), () => _ = PantallaCompleta.Ofrecer(), Ui.Oro);
        _completa.IsVisible = !Licencia.Completa;
        Grid.SetColumn(_completa, 2); cabecera.Children.Add(_completa);
        var info = Ui.BotonIcono(Ui.Iconos.Info, T("Cómo funciona: compartir y copia de seguridad"), () => _ = ComoCompartir());
        Grid.SetColumn(info, 3); cabecera.Children.Add(info);
        SetDock(cabecera, Dock.Top); Children.Add(cabecera);

        var abajo = Ui.LetraGrande
            ? new Grid { RowDefinitions = new RowDefinitions("Auto,10,Auto"), Margin = new Thickness(16, 8, 16, 16), MaxWidth = AnchoMaximo }
            : new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), Margin = new Thickness(16, 8, 16, 16), MaxWidth = AnchoMaximo };
        var nuevo = Ui.Boton(T("Nuevo árbol"), () => _ = Nuevo(), principal: true, icono: Ui.Iconos.Nuevo);
        nuevo.HorizontalAlignment = HorizontalAlignment.Stretch;
        var abrir = Ui.Boton(T("Abrir archivo"), () => _ = AbrirArchivo(), icono: Ui.Iconos.Carpeta);
        abrir.HorizontalAlignment = HorizontalAlignment.Stretch;
        abajo.Children.Add(nuevo);
        if (Ui.LetraGrande) Grid.SetRow(abrir, 2); else Grid.SetColumn(abrir, 2);
        abajo.Children.Add(abrir);
        SetDock(abajo, Dock.Bottom); Children.Add(abajo);

        Children.Add(new ScrollViewer { Content = _lista });
        AttachedToVisualTree += (_, _) => { Recargar(); Licencia.Cambiada += AlCambiarLicencia; };
        DetachedFromVisualTree += (_, _) => Licencia.Cambiada -= AlCambiarLicencia;
    }

    private void AlCambiarLicencia() => _completa.IsVisible = !Licencia.Completa;

    public bool Atras() => false;

    public void Recargar()
    {
        _lista.Children.Clear();
        var arboles = App.Biblioteca.Listar();
        if (arboles.Count == 0)
        {
            var vacio = new StackPanel { Spacing = 14, Margin = new Thickness(4, 30, 4, 0) };
            vacio.Children.Add(Ui.Titulo(T("Aún no tienes ningún árbol")));
            vacio.Children.Add(Ui.Parrafo(T(
                "Crea uno nuevo empezando por ti, o abre el que te haya mandado un familiar: tócalo en WhatsApp, en el correo " +
                "o en tus archivos y elige «Ramo».\n\nTodo se guarda solo en este teléfono: no hace falta cuenta ni conexión.")));
            var ejemplo = Ui.Boton(T("Ver un árbol de ejemplo"), () => _ = AbrirEjemplo(), icono: Ui.Iconos.Galeria);
            ejemplo.HorizontalAlignment = HorizontalAlignment.Left;
            vacio.Children.Add(ejemplo);
            if (App.Plataforma.CarpetaCopia is { } copia)
            {
                // si se ha reinstalado la aplicación o se han borrado sus datos, los árboles siguen en la copia
                vacio.Children.Add(Ui.Parrafo(T("¿Ya tenías árboles en este teléfono? Si se borró la aplicación o sus datos, hay una copia en «{0}».", copia), 15));
                var recuperar = Ui.Boton(T("Recuperar mis árboles"), () => _ = Recuperar(), icono: Ui.Iconos.Carpeta);
                recuperar.HorizontalAlignment = HorizontalAlignment.Left;
                vacio.Children.Add(recuperar);
            }
            _lista.Children.Add(vacio);
            return;
        }
        foreach (var e in arboles) _lista.Children.Add(Fila(e));
    }

    private Control Fila(EntradaArbol e)
    {
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
        textos.Children.Add(new TextBlock { Text = e.Nombre, FontSize = Ui.L(18), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        textos.Children.Add(new TextBlock { Text = $"{Principal.Personas(e.Personas)} · {Fecha(e.Fecha)}", FontSize = Ui.L(14), Foreground = Ui.Suave, TextWrapping = TextWrapping.Wrap });
        var menu = Ui.BotonIcono(Ui.Iconos.Menu, T("Más opciones"), () => _ = MenuArbol(e));
        menu.HorizontalAlignment = HorizontalAlignment.Right; menu.VerticalAlignment = VerticalAlignment.Center;
        menu.Margin = new Thickness(0, 0, 6, 0);
        // la tarjeta abre el árbol; el ⋮ va encima de ella, no dentro: un botón dentro de otro hacía que tocar el ⋮
        // abriera también el árbol (y que después la tarjeta no respondiera al primer toque)
        var b = new Button
        {
            Content = textos, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Ui.Panel, CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12, 16 + 44 * Ui.EscalaIcono, 12),
        };
        b.Click += (_, _) => Abrir(e.Ruta);
        return new Grid { Children = { b, menu } };
    }

    private static string Fecha(DateTime f)
    {
        var hoy = DateTime.Today;
        if (f.Date == hoy) return T("hoy, {0}", f.ToString("HH:mm"));
        if (f.Date == hoy.AddDays(-1)) return T("ayer");
        // el formato también se traduce: «18 oct», «Oct 18», «18. Okt.»
        return f.ToString(f.Year == hoy.Year ? T("d MMM") : T("d MMM yyyy"), Cultura);
    }

    public static void Abrir(string ruta)
    {
        try { Principal.Actual.Ir(new PantallaArbol(ruta)); }
        catch (Exception ex) { _ = Principal.Actual.Error(T("No se pudo abrir el árbol"), ex); }
    }

    internal async Task Nuevo()
    {
        var p = Principal.Actual;
        if (!Licencia.PuedeCrearArbol && !await PantallaCompleta.PorArboles()) return;
        var nombre = await p.PedirTexto(T("Tu nombre"), "", T("Empezar"), T("Nombre de la persona principal"));
        if (nombre == null) return;
        var a = Arbol.Nuevo("", nombre);
        var titulo = string.IsNullOrWhiteSpace(nombre) ? T("Mi familia") : T("Familia de {0}", nombre);
        a.Nombre = titulo;
        var ruta = App.Biblioteca.GuardarNuevo(a, titulo);
        Abrir(ruta);
    }

    private async Task AbrirArchivo()
    {
        var almacen = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (almacen == null) return;
        var archivos = await almacen.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = T("Abrir árbol"), AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(T("Árbol genealógico")) { Patterns = new[] { "*.ramo", "*.tree", "*.json", "*.ged", "*.gedcom" }, MimeTypes = new[] { "application/octet-stream", "application/json", "application/x-gedcom", "text/x-gedcom", "text/plain" } },
                FilePickerFileTypes.All,
            },
        });
        foreach (var f in archivos)
        {
            try
            {
                await using var s = await f.OpenReadAsync();
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms);
                App.Recibir(new ArchivoRecibido(f.Name, ms.ToArray()));
            }
            catch (Exception ex) { await Principal.Actual.Error(T("No se pudo leer {0}", f.Name), ex); }
        }
    }

    /// <summary>
    /// Trae a la biblioteca los árboles de la copia de Documentos (u otros .ramo que se elijan), tal cual: sin preguntar
    /// por fusiones y sin duplicar los que ya estén.
    /// </summary>
    private async Task Recuperar()
    {
        IReadOnlyList<ArchivoRecibido> archivos;
        if (App.Plataforma.PuedeLeerCopias)
        {
            // Android: el selector de carpetas se abre ya en la copia; con «Usar esta carpeta» se recuperan todos
            await Principal.Actual.Avisar(T("Recuperar mis árboles"),
                T("Se abrirá la carpeta «{0}». Pulsa «Usar esta carpeta» y después «Permitir»: se recuperarán todos los árboles que haya en ella.",
                  App.Plataforma.CarpetaCopia));
            var leidos = await App.Plataforma.LeerCopias();
            if (leidos == null) return;
            archivos = leidos;
            if (archivos.Count == 0)
            {
                await Principal.Actual.Avisar(T("No hay árboles en esa carpeta"),
                    T("Comprueba que es «{0}». Si guardaste copias en otro sitio, usa «Abrir archivo».", App.Plataforma.CarpetaCopia));
                return;
            }
        }
        else
        {
            var almacen = TopLevel.GetTopLevel(this)?.StorageProvider;
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

        int recuperados = 0;
        var fallos = new List<string>();
        foreach (var f in archivos)
        {
            try
            {
                var texto = new System.Text.UTF8Encoding(false).GetString(f.Datos).TrimStart('\uFEFF');
                App.Biblioteca.Importar(ArbolJson.Deserializar(texto, out _), f.Nombre);
                recuperados++;
            }
            catch { fallos.Add(f.Nombre); }
        }
        Recargar();
        if (fallos.Count > 0)
            await Principal.Actual.Avisar(T("Algunos archivos no se pudieron recuperar"), T("No son árboles genealógicos o están dañados: {0}", string.Join(", ", fallos)));
        else if (recuperados > 0)
            Principal.Actual.Aviso(N(recuperados, "Árbol recuperado", "{0} árboles recuperados"));
    }

    /// <summary>El árbol de ejemplo en el idioma de ahora: el nombre del árbol, los de las personas (Rosa), sus apellidos,
    /// dónde nacieron y sus historias (en inglés, la familia Smith de Boston, no la familia García de Ávila).</summary>
    internal static Arbol TraducirEjemplo(Arbol a)
    {
        a.Nombre = T(a.Nombre);
        foreach (var q in a.Personas)
        {
            q.Nombre = T(q.Nombre);
            if (q.Apellidos.Length > 0) q.Apellidos = T(q.Apellidos);
            if (!string.IsNullOrEmpty(q.LugarNacimiento)) q.LugarNacimiento = T(q.LugarNacimiento);
            if (q.Historia.Length > 0) q.Historia = T(q.Historia);
        }
        return a;
    }

    internal async Task AbrirEjemplo()
    {
        foreach (var nombre in new[] { "familia-perez.ramo", "familia-garcia.ramo" })
        {
            using var s = AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/Ejemplos/" + nombre));
            using var lector = new StreamReader(s);
            var a = TraducirEjemplo(ArbolJson.Deserializar(await lector.ReadToEndAsync(), out _));
            var ruta = App.Biblioteca.Importar(a, nombre);
            if (nombre == "familia-garcia.ramo") Abrir(ruta);
        }
    }

    private async Task MenuArbol(EntradaArbol e)
    {
        var p = Principal.Actual;
        var compartir = new Opcion(T("Compartir"), Ui.Iconos.Compartir, T("El archivo .ramo, para abrirlo y editarlo con esta aplicación"));
        var gedcom = new Opcion(T("Exportar a GEDCOM"), Ui.Iconos.Exportar, T("Para otros programas de genealogía. Sin fotos"));
        var renombrar = new Opcion(T("Cambiar el nombre"), Ui.Iconos.Editar);
        var copia = new Opcion(T("Guardar una copia en…"), Ui.Iconos.Guardar, T("En Descargas, en Drive…"));
        var borrar = new Opcion(T("Borrar del teléfono"), Ui.Iconos.Papelera, Peligro: true);
        var r = await p.Menu(e.Nombre, new[] { compartir, gedcom, renombrar, copia, Opcion.Linea, borrar });
        try
        {
            if (r == compartir) await Compartir.Arbol(e.Ruta, e.Nombre);
            else if (r == gedcom) await Compartir.Gedcom(e.Ruta, e.Nombre);
            else if (r == renombrar)
            {
                var nombre = await p.PedirTexto(T("Nombre del árbol"), e.Nombre);
                if (!string.IsNullOrWhiteSpace(nombre) && nombre != e.Nombre) { App.Biblioteca.Renombrar(e.Ruta, nombre); Recargar(); }
            }
            else if (r == copia) await Compartir.GuardarCopia(this, e.Ruta, e.Nombre);
            else if (r == borrar)
            {
                string respaldo = Biblioteca.CopiaActiva
                    ? T("Su copia de seguridad en «{0}» se queda, por si te hiciera falta.", App.Plataforma.CarpetaCopia)
                    : T("No se puede deshacer.");
                int ok = await p.Preguntar(T("¿Borrar «{0}»?", e.Nombre),
                    T("Se borrará de la aplicación. Si se lo has mandado a alguien, su copia no se borra.") + " " + respaldo,
                    T("Borrar"), T("Cancelar"));
                if (ok == 0) { App.Biblioteca.Borrar(e.Ruta); Recargar(); p.Aviso(T("Árbol borrado")); }
            }
        }
        catch (Exception ex) { await p.Error(T("No se pudo completar"), ex); }
    }

    internal async Task ComoCompartir()
    {
        var copia = App.Plataforma.CarpetaCopia;
        string texto = T(
            "Los árboles se guardan solo en este teléfono. Para mandarle uno a un familiar, toca «⋮ → Compartir»: se manda el " +
            "archivo .ramo por WhatsApp, Telegram, correo, Bluetooth o Quick Share, como una foto.\n\n" +
            "Cuando te manden uno, tócalo y elige «Ramo». Si ya tienes ese árbol (o uno de la misma familia), " +
            "puedes fusionarlos: se añaden las personas nuevas y se completan los datos que te falten.");
        if (copia == null) { await Principal.Actual.Avisar(T("Compartir sin internet"), texto); return; }
        bool activa = Preferencias.Actual.CopiaEnDocumentos;
        texto += "\n\n" + (activa
            ? T("COPIA DE SEGURIDAD: cada vez que cambias un árbol se guarda una copia en «{0}». Si se borra la aplicación " +
                "o sus datos, desde ahí se recuperan. Esa carpeta la pueden ver otras aplicaciones con permiso para leer tus archivos.", copia)
            : T("COPIA DE SEGURIDAD: desactivada. Si la activas, cada vez que cambies un árbol se guardará una copia en «{0}», " +
                "para poder recuperarlos si se borra la aplicación o sus datos.", copia));
        int r = await Principal.Actual.Preguntar(T("Cómo funciona"), texto, T("Aceptar"),
            activa ? T("Desactivar la copia") : T("Activar la copia"), T("Recuperar árboles…"));
        if (r == 1)
        {
            Preferencias.Actual.CopiaEnDocumentos = !activa;
            Preferencias.Actual.Guardar();
            if (!activa) App.Biblioteca.CopiarTodos();
            Principal.Actual.Aviso(activa ? T("Copia de seguridad desactivada (la que ya había se queda)") : T("Copia de seguridad activada"));
        }
        else if (r == 2) await Recuperar();
    }
}
