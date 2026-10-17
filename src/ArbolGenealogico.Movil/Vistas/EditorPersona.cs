using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>Lo que devuelve el editor: la persona con los cambios (null si se cancela) o que se quiere eliminar.</summary>
public sealed record ResultadoEditor(Persona? Persona, bool Eliminar = false);

/// <summary>La ficha de una persona, a pantalla completa: foto, nombre, apellidos, sexo, historia y árbol enlazado.</summary>
public static class EditorPersona
{
    public static Task<ResultadoEditor> Abrir(Persona original, Arbol arbol, bool nueva) =>
        Principal.Actual.Hoja<ResultadoEditor>(cerrar => Crear(original.Clonar(), arbol, nueva, cerrar), new ResultadoEditor(null));

    private static Control Crear(Persona p, Arbol arbol, bool nueva, Action<ResultadoEditor> cerrar)
    {
        var raiz = new DockPanel();

        // barra: cancelar, título y guardar
        var barra = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Background = Ui.Barra, Height = 56 };
        barra.Children.Add(Ui.BotonIcono(Ui.Iconos.Cerrar, "Cancelar", () => cerrar(new ResultadoEditor(null))));
        var titulo = Ui.Titulo(nueva ? "Nueva persona" : "Ficha", 19);
        titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 1); barra.Children.Add(titulo);
        var guardar = Ui.Boton("Guardar", () => { }, principal: true);
        guardar.Margin = new Thickness(0, 0, 10, 0); guardar.Padding = new Thickness(18, 8);
        guardar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(guardar, 2); barra.Children.Add(guardar);
        DockPanel.SetDock(barra, Dock.Top); raiz.Children.Add(barra);

        var pila = new StackPanel { Spacing = 14, Margin = new Thickness(20, 18, 20, 30), MaxWidth = 560 };
        raiz.Children.Add(new ScrollViewer { Content = pila });

        // foto
        var marcoFoto = new ContentControl { HorizontalAlignment = HorizontalAlignment.Center };
        void PintarFoto() => marcoFoto.Content = Ui.Retrato(p, 128);
        PintarFoto();
        pila.Children.Add(marcoFoto);
        var botonesFoto = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var quitar = Ui.Boton("Quitar", () => { p.Foto = null; PintarFoto(); });
        void Estado() => quitar.IsVisible = p.Foto != null;
        async Task Poner(byte[]? datos)
        {
            if (datos == null) return;
            try
            {
                var f = Fotos.Preparar(datos);
                if (f == null) { await Principal.Actual.Avisar("No se pudo usar la foto", "El archivo no es una imagen que se pueda leer."); return; }
                p.Foto = f; PintarFoto(); Estado();
            }
            catch (Exception ex) { await Principal.Actual.Error("No se pudo usar la foto", ex); }
        }
        var galeria = Ui.Boton("Galería", async () => await Poner(await ElegirImagen(pila)), icono: Ui.Iconos.Galeria);
        galeria.Margin = new Thickness(4); botonesFoto.Children.Add(galeria);
        if (App.Plataforma.HayCamara)
        {
            var camara = Ui.Boton("Cámara", async () => await Poner(await App.Plataforma.HacerFoto()), icono: Ui.Iconos.Camara);
            camara.Margin = new Thickness(4); botonesFoto.Children.Add(camara);
        }
        quitar.Margin = new Thickness(4); botonesFoto.Children.Add(quitar);
        Estado();
        pila.Children.Add(botonesFoto);

        // textos
        var nombre = new TextBox { Text = p.Nombre, Watermark = "Nombre" };
        var apellidos = new TextBox { Text = p.Apellidos, Watermark = "Apellidos" };
        // teclado del móvil: nombres con mayúscula; «Siguiente» en el nombre pasa a los apellidos
        foreach (var t in new[] { nombre, apellidos })
        {
            TextInputOptions.SetContentType(t, TextInputContentType.Name);
            TextInputOptions.SetAutoCapitalization(t, true);
        }
        TextInputOptions.SetReturnKeyType(nombre, TextInputReturnKeyType.Next);
        nombre.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { apellidos.Focus(); e.Handled = true; } };
        TextInputOptions.SetReturnKeyType(apellidos, TextInputReturnKeyType.Done);
        pila.Children.Add(Etiqueta("Nombre")); pila.Children.Add(nombre);
        pila.Children.Add(Etiqueta("Apellidos")); pila.Children.Add(apellidos);

        // sexo
        pila.Children.Add(Etiqueta("Sexo"));
        var sexo = p.Sexo;
        var fila = new UniformGrid { Columns = 3 };
        var opciones = new (Sexo S, string Texto)[] { (Sexo.Hombre, "Hombre"), (Sexo.Mujer, "Mujer"), (Sexo.Desconocido, "Sin indicar") };
        var botones = new List<ToggleButton>();
        foreach (var (s, t) in opciones)
        {
            var b = new ToggleButton
            {
                Content = t, IsChecked = s == sexo, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 10), CornerRadius = new CornerRadius(10),
            };
            b.Click += (_, _) => { sexo = s; foreach (var o in botones) o.IsChecked = o == b; };
            botones.Add(b); fila.Children.Add(b);
        }
        pila.Children.Add(fila);

        // historia
        pila.Children.Add(Etiqueta("Su historia"));
        var historia = new TextBox
        {
            Text = p.Historia, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 130,
            Watermark = "Dónde nació, a qué se dedicó, anécdotas…",
        };
        // varias líneas: la tecla Intro del teclado del móvil salta de línea en vez de cerrar el teclado («Hecho»)
        TextInputOptions.SetMultiline(historia, true);
        TextInputOptions.SetReturnKeyType(historia, TextInputReturnKeyType.Return);
        TextInputOptions.SetAutoCapitalization(historia, true);
        pila.Children.Add(historia);
        // al escribir al final, la caja crece hacia abajo: que la última línea siga a la vista (por encima del teclado)
        historia.TextChanged += (_, _) =>
        {
            if (historia.IsFocused && historia.CaretIndex >= (historia.Text?.Length ?? 0))
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    historia.BringIntoView(new Rect(0, Math.Max(0, historia.Bounds.Height - 48), historia.Bounds.Width, 48)),
                    Avalonia.Threading.DispatcherPriority.Background);
        };

        // árbol enlazado (el árbol propio de la persona, normalmente su familia política)
        if (p.Id != arbol.RaizId)
        {
            pila.Children.Add(Etiqueta("Su propio árbol"));
            var enlace = new TextBlock { FontSize = 15, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            void PintarEnlace() => enlace.Text = string.IsNullOrEmpty(p.ArbolEnlazado) ? "Ninguno" : Path.GetFileNameWithoutExtension(p.ArbolEnlazado);
            PintarEnlace();
            var filaEnlace = new DockPanel();
            var elegir = Ui.Boton("Elegir", async () =>
            {
                var lista = App.Biblioteca.Listar();
                var ops = lista.Select(e => new Opcion(e.Nombre, Ui.Iconos.Enlace, Principal.Personas(e.Personas))).ToList();
                var quita = new Opcion("Sin árbol enlazado", Ui.Iconos.Cerrar);
                if (!string.IsNullOrEmpty(p.ArbolEnlazado)) { ops.Add(Opcion.Linea); ops.Add(quita); }
                var r = await Principal.Actual.Menu("El árbol de " + Nombres.Mostrar(p), ops);
                if (r == null) return;
                p.ArbolEnlazado = r == quita ? null : lista[ops.IndexOf(r)].Archivo;
                PintarEnlace();
            });
            DockPanel.SetDock(elegir, Dock.Right);
            filaEnlace.Children.Add(elegir);
            filaEnlace.Children.Add(enlace);
            pila.Children.Add(filaEnlace);
        }

        if (!nueva && p.Id != arbol.RaizId)
        {
            var eliminar = new Button
            {
                Content = new TextBlock { Text = "Eliminar a esta persona del árbol", Foreground = Ui.Rojo }, Classes = { "secundario" },
                HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 22, 0, 0),
            };
            eliminar.Click += (_, _) => cerrar(new ResultadoEditor(null, Eliminar: true));
            pila.Children.Add(eliminar);
        }

        guardar.Click += (_, _) =>
        {
            p.Nombre = nombre.Text?.Trim() ?? "";
            p.Apellidos = apellidos.Text?.Trim() ?? "";
            p.Sexo = sexo;
            p.Historia = historia.Text?.Trim() ?? "";
            cerrar(new ResultadoEditor(p));
        };
        if (nueva) nombre.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => nombre.Focus(), Avalonia.Threading.DispatcherPriority.Input);
        return raiz;
    }

    private static TextBlock Etiqueta(string s) => new() { Text = s, FontSize = 13, Foreground = Ui.Suave, Margin = new Thickness(2, 6, 0, -6) };

    private static async Task<byte[]?> ElegirImagen(Control desde)
    {
        var almacen = TopLevel.GetTopLevel(desde)?.StorageProvider;
        if (almacen == null) return null;
        var archivos = await almacen.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Elegir foto", AllowMultiple = false, FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
        });
        if (archivos.Count == 0) return null;
        await using var s = await archivos[0].OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }
}
