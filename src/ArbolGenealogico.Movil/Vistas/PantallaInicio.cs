using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>«Mis árboles»: los árboles guardados en el teléfono, para abrirlos, compartirlos o crear uno nuevo.</summary>
public sealed class PantallaInicio : DockPanel, IPantalla
{
    private readonly StackPanel _lista = new() { Spacing = 10, Margin = new Thickness(16, 8, 16, 24) };

    public PantallaInicio()
    {
        var cabecera = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(16, 14, 8, 6),
        };
        var icono = new Image { Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/icono.png"))), Width = 40, Height = 40, Margin = new Thickness(0, 0, 12, 0) };
        cabecera.Children.Add(icono);
        var titulo = Ui.Titulo("Mis árboles", 24);
        titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 1); cabecera.Children.Add(titulo);
        var info = Ui.BotonIcono(Ui.Iconos.Info, "Cómo se comparten los árboles", () => _ = ComoCompartir());
        Grid.SetColumn(info, 2); cabecera.Children.Add(info);
        SetDock(cabecera, Dock.Top); Children.Add(cabecera);

        var abajo = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), Margin = new Thickness(16, 8, 16, 16) };
        var nuevo = Ui.Boton("Nuevo árbol", () => _ = Nuevo(), principal: true, icono: Ui.Iconos.Nuevo);
        nuevo.HorizontalAlignment = HorizontalAlignment.Stretch;
        var abrir = Ui.Boton("Abrir archivo", () => _ = AbrirArchivo(), icono: Ui.Iconos.Carpeta);
        abrir.HorizontalAlignment = HorizontalAlignment.Stretch;
        abajo.Children.Add(nuevo);
        Grid.SetColumn(abrir, 2); abajo.Children.Add(abrir);
        SetDock(abajo, Dock.Bottom); Children.Add(abajo);

        Children.Add(new ScrollViewer { Content = _lista });
        AttachedToVisualTree += (_, _) => Recargar();
    }

    public bool Atras() => false;

    public void Recargar()
    {
        _lista.Children.Clear();
        var arboles = App.Biblioteca.Listar();
        if (arboles.Count == 0)
        {
            var vacio = new StackPanel { Spacing = 14, Margin = new Thickness(4, 30, 4, 0) };
            vacio.Children.Add(Ui.Titulo("Aún no tienes ningún árbol"));
            vacio.Children.Add(Ui.Parrafo(
                "Crea uno nuevo empezando por ti, o abre el que te haya mandado un familiar: tócalo en WhatsApp, en el correo " +
                "o en tus archivos y elige «Árbol Genealógico».\n\nTodo se guarda solo en este teléfono: no hace falta cuenta ni conexión."));
            var ejemplo = Ui.Boton("Ver un árbol de ejemplo", () => _ = AbrirEjemplo(), icono: Ui.Iconos.Galeria);
            ejemplo.HorizontalAlignment = HorizontalAlignment.Left;
            vacio.Children.Add(ejemplo);
            _lista.Children.Add(vacio);
            return;
        }
        foreach (var e in arboles) _lista.Children.Add(Fila(e));
    }

    private Control Fila(EntradaArbol e)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
        textos.Children.Add(new TextBlock { Text = e.Nombre, FontSize = 18, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        textos.Children.Add(new TextBlock { Text = $"{Principal.Personas(e.Personas)} · {Fecha(e.Fecha)}", FontSize = 14, Foreground = Ui.Suave });
        g.Children.Add(textos);
        var menu = Ui.BotonIcono(Ui.Iconos.Menu, "Más opciones", () => _ = MenuArbol(e));
        Grid.SetColumn(menu, 1); g.Children.Add(menu);
        var b = new Button
        {
            Content = g, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Ui.Panel, CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12, 6, 12),
        };
        b.Click += (_, _) => Abrir(e.Ruta);
        return b;
    }

    private static string Fecha(DateTime f)
    {
        var hoy = DateTime.Today;
        if (f.Date == hoy) return "hoy, " + f.ToString("HH:mm");
        if (f.Date == hoy.AddDays(-1)) return "ayer";
        return f.ToString(f.Year == hoy.Year ? "d MMM" : "d MMM yyyy", new System.Globalization.CultureInfo("es-ES"));
    }

    public static void Abrir(string ruta)
    {
        try { Principal.Actual.Ir(new PantallaArbol(ruta)); }
        catch (Exception ex) { _ = Principal.Actual.Error("No se pudo abrir el árbol", ex); }
    }

    private async Task Nuevo()
    {
        var p = Principal.Actual;
        var nombre = await p.PedirTexto("Tu nombre", "", "Empezar", "Nombre de la persona principal");
        if (nombre == null) return;
        var a = Arbol.Nuevo("", nombre);
        var titulo = string.IsNullOrWhiteSpace(nombre) ? "Mi familia" : "Familia de " + nombre;
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
            Title = "Abrir árbol", AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Árbol genealógico") { Patterns = new[] { "*.tree", "*.json" }, MimeTypes = new[] { "application/octet-stream", "application/json" } },
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
            catch (Exception ex) { await Principal.Actual.Error("No se pudo leer " + f.Name, ex); }
        }
    }

    private async Task AbrirEjemplo()
    {
        foreach (var nombre in new[] { "familia-perez.tree", "familia-garcia.tree" })
        {
            using var s = AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/Ejemplos/" + nombre));
            using var lector = new StreamReader(s);
            var a = ArbolJson.Deserializar(await lector.ReadToEndAsync(), out _);
            var ruta = App.Biblioteca.Importar(a, nombre);
            if (nombre == "familia-garcia.tree") Abrir(ruta);
        }
    }

    private async Task MenuArbol(EntradaArbol e)
    {
        var p = Principal.Actual;
        var compartir = new Opcion("Compartir", Ui.Iconos.Compartir, "Mandarlo por WhatsApp, correo, Bluetooth…");
        var renombrar = new Opcion("Cambiar el nombre", Ui.Iconos.Editar);
        var copia = new Opcion("Guardar una copia en…", Ui.Iconos.Guardar, "En Descargas, en Drive…");
        var borrar = new Opcion("Borrar del teléfono", Ui.Iconos.Papelera, Peligro: true);
        var r = await p.Menu(e.Nombre, new[] { compartir, renombrar, copia, Opcion.Linea, borrar });
        try
        {
            if (r == compartir) await Compartir.Arbol(e.Ruta, e.Nombre);
            else if (r == renombrar)
            {
                var nombre = await p.PedirTexto("Nombre del árbol", e.Nombre);
                if (!string.IsNullOrWhiteSpace(nombre) && nombre != e.Nombre) { App.Biblioteca.Renombrar(e.Ruta, nombre); Recargar(); }
            }
            else if (r == copia) await Compartir.GuardarCopia(this, e.Ruta, e.Nombre);
            else if (r == borrar)
            {
                int ok = await p.Preguntar("¿Borrar «" + e.Nombre + "»?",
                    "Se borrará de este teléfono. Si se lo has mandado a alguien, su copia no se borra. No se puede deshacer.",
                    "Borrar", "Cancelar");
                if (ok == 0) { App.Biblioteca.Borrar(e.Ruta); Recargar(); p.Aviso("Árbol borrado"); }
            }
        }
        catch (Exception ex) { await p.Error("No se pudo completar", ex); }
    }

    private static Task ComoCompartir() => Principal.Actual.Avisar("Compartir sin internet",
        "Los árboles se guardan solo en este teléfono. Para mandarle uno a un familiar, toca «⋮ → Compartir»: se manda el " +
        "archivo .tree por WhatsApp, Telegram, correo, Bluetooth o Quick Share, como una foto.\n\n" +
        "Cuando te manden uno, tócalo y elige «Árbol Genealógico». Si ya tienes ese árbol (o uno de la misma familia), " +
        "puedes fusionarlos: se añaden las personas nuevas y se completan los datos que te falten.\n\n" +
        "El mismo archivo se abre en la aplicación del ordenador.");
}
