using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.TextInput;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>Lo que devuelve el editor: la persona con los cambios (null si se cancela) o que se quiere eliminar.</summary>
public sealed record ResultadoEditor(Persona? Persona, bool Eliminar = false);

/// <summary>La ficha de una persona, a pantalla completa: foto, nombre, apellidos, sexo, historia y árbol enlazado.</summary>
public static class EditorPersona
{
    public static Task<ResultadoEditor> Abrir(Persona original, Arbol arbol, bool nueva) =>
        Principal.Actual.Hoja<ResultadoEditor>(cerrar => Crear(original, original.Clonar(), arbol, nueva, cerrar), new ResultadoEditor(null));

    private static Control Crear(Persona original, Persona p, Arbol arbol, bool nueva, Action<ResultadoEditor> cerrar)
    {
        var raiz = new PanelConAtras();
        // al salir con «Atrás» o con la ✕, si hay cambios se pregunta si guardarlos (se asigna más abajo)
        Func<Task> salir = () => { cerrar(new ResultadoEditor(null)); return Task.CompletedTask; };
        raiz.AlPedirAtras = () => { _ = salir(); return true; };

        // barra: cancelar, título y guardar
        var barra = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Background = Ui.Barra, MinHeight = 56 };
        barra.Children.Add(Ui.BotonIcono(Ui.Iconos.Cerrar, T("Cerrar"), () => _ = salir()));
        var titulo = Ui.Titulo(nueva ? T("Nueva persona") : T("Ficha"), 19);
        titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 1); barra.Children.Add(titulo);
        var guardar = Ui.Boton(T("Guardar"), () => { }, principal: true);
        guardar.Margin = new Thickness(0, 0, 10, 0); guardar.Padding = new Thickness(18, 8);
        guardar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(guardar, 2); barra.Children.Add(guardar);
        DockPanel.SetDock(barra, Dock.Top); raiz.Children.Add(barra);

        var pila = new StackPanel { Spacing = 14, Margin = new Thickness(20, 18, 20, 30), MaxWidth = 560 };
        raiz.Children.Add(new ScrollViewer { Content = pila });

        // foto
        var marcoFoto = new ContentControl { HorizontalAlignment = HorizontalAlignment.Center, Background = Brushes.Transparent };
        // tocar la foto la enseña en grande
        marcoFoto.Tapped += (_, e) => { if (VerFoto.Abrir(p)) e.Handled = true; };
        void PintarFoto() => marcoFoto.Content = Ui.Retrato(p, 128);
        PintarFoto();
        pila.Children.Add(marcoFoto);
        var botonesFoto = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        // la imagen de la que salió la foto mientras está abierta la ficha, para volver a encuadrarla sin perder nada de
        // lo que quedó fuera del círculo (la foto que se guarda es solo el cuadrado elegido)
        SkiaSharp.SKBitmap? imagenFoto = null;
        SkiaSharp.SKRect recorte = default;
        Button quitar = null!, encuadrar = null!;
        quitar = Ui.Boton(T("Quitar"), () => { p.Foto = null; imagenFoto = null; PintarFoto(); Estado(); });
        void Estado() => quitar.IsVisible = encuadrar.IsVisible = p.Foto != null;
        async Task Encuadrar(SkiaSharp.SKBitmap img, SkiaSharp.SKRect? inicial)
        {
            if (await EncuadrarFoto.Abrir(img, inicial) is not { } r) return;
            p.Foto = Fotos.Recortar(img, r);
            imagenFoto = img; recorte = r;
            PintarFoto(); Estado();
        }
        async Task Poner(byte[]? datos)
        {
            if (datos == null) return;
            try
            {
                var img = Fotos.Abrir(datos);
                if (img == null) { await Principal.Actual.Avisar(T("No se pudo usar la foto"), T("El archivo no es una imagen que se pueda leer.")); return; }
                await Encuadrar(img, null);
            }
            catch (Exception ex) { await Principal.Actual.Error(T("No se pudo usar la foto"), ex); }
        }
        encuadrar = Ui.Boton(T("Encuadrar"), async () =>
        {
            try
            {
                if (imagenFoto != null) await Encuadrar(imagenFoto, recorte);
                else if (Fotos.Abrir(p.Foto) is { } guardada) await Encuadrar(guardada, null);
            }
            catch (Exception ex) { await Principal.Actual.Error(T("No se pudo encuadrar la foto"), ex); }
        }, icono: Ui.Iconos.Encuadre);
        var galeria = Ui.Boton(T("Galería"), async () => await Poner(await ElegirImagen(pila)), icono: Ui.Iconos.Galeria);
        galeria.Margin = new Thickness(4); botonesFoto.Children.Add(galeria);
        if (App.Plataforma.HayCamara)
        {
            var camara = Ui.Boton(T("Cámara"), async () => await Poner(await App.Plataforma.HacerFoto()), icono: Ui.Iconos.Camara);
            camara.Margin = new Thickness(4); botonesFoto.Children.Add(camara);
        }
        encuadrar.Margin = new Thickness(4); botonesFoto.Children.Add(encuadrar);
        quitar.Margin = new Thickness(4); botonesFoto.Children.Add(quitar);
        Estado();
        pila.Children.Add(botonesFoto);

        // textos
        var nombre = new TextBox { Text = p.Nombre, PlaceholderText = T("Nombre") };
        var apellidos = new TextBox { Text = p.Apellidos, PlaceholderText = T("Apellidos") };
        // teclado del móvil: nombres con mayúscula; «Siguiente» en el nombre pasa a los apellidos
        foreach (var t in new[] { nombre, apellidos })
        {
            TextInputOptions.SetContentType(t, TextInputContentType.Name);
            TextInputOptions.SetAutoCapitalization(t, true);
        }
        TextInputOptions.SetReturnKeyType(nombre, TextInputReturnKeyType.Next);
        nombre.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { apellidos.Focus(); e.Handled = true; } };
        TextInputOptions.SetReturnKeyType(apellidos, TextInputReturnKeyType.Done);
        pila.Children.Add(Etiqueta(T("Nombre"))); pila.Children.Add(nombre);
        pila.Children.Add(Etiqueta(T("Apellidos"))); pila.Children.Add(apellidos);

        // sexo
        pila.Children.Add(Etiqueta(T("Sexo")));
        var sexo = p.Sexo;
        var fila = new UniformGrid { Columns = Ui.LetraGrande ? 1 : 3 };
        var opciones = new (Sexo S, string Texto)[] { (Sexo.Hombre, T("Hombre")), (Sexo.Mujer, T("Mujer")), (Sexo.Desconocido, T("Sin indicar")) };
        var botones = new List<ToggleButton>();
        foreach (var (s, t) in opciones)
        {
            var b = new ToggleButton
            {
                Content = t, IsChecked = s == sexo, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = Ui.LetraGrande ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 6, 0), Padding = new Thickness(6, 10), CornerRadius = new CornerRadius(10),
            };
            b.Click += (_, _) => { sexo = s; foreach (var o in botones) o.IsChecked = o == b; };
            botones.Add(b); fila.Children.Add(b);
        }
        pila.Children.Add(fila);

        // fechas: basta con el año; el mes y el día, si se saben
        var (filaNacimiento, leerNacimiento) = CampoFecha(p.Nacimiento);
        var (filaDefuncion, leerDefuncion) = CampoFecha(p.Defuncion);
        pila.Children.Add(Etiqueta(T("Nacimiento"))); pila.Children.Add(filaNacimiento);
        pila.Children.Add(Etiqueta(T("Defunción"))); pila.Children.Add(filaDefuncion);

        // historia
        pila.Children.Add(Etiqueta(T("Su historia")));
        var historia = new TextBox
        {
            Text = p.Historia, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 130,
            PlaceholderText = T("Dónde nació, a qué se dedicó, anécdotas…"),
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
            pila.Children.Add(Etiqueta(T("Su propio árbol")));
            var enlace = new TextBlock { FontSize = Ui.L(15), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            void PintarEnlace() => enlace.Text = string.IsNullOrEmpty(p.ArbolEnlazado) ? T("Ninguno") : Path.GetFileNameWithoutExtension(p.ArbolEnlazado);
            PintarEnlace();
            var filaEnlace = new DockPanel();
            var elegir = Ui.Boton(T("Elegir"), async () =>
            {
                var lista = App.Biblioteca.Listar();
                var ops = lista.Select(e => new Opcion(e.Nombre, Ui.Iconos.Enlace, Principal.Personas(e.Personas))).ToList();
                var quita = new Opcion(T("Sin árbol enlazado"), Ui.Iconos.Cerrar);
                if (!string.IsNullOrEmpty(p.ArbolEnlazado)) { ops.Add(Opcion.Linea); ops.Add(quita); }
                var r = await Principal.Actual.Menu(T("El árbol de {0}", Nombres.Mostrar(p)), ops);
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
                Content = new TextBlock { Text = T("Eliminar a esta persona del árbol"), Foreground = Ui.Rojo }, Classes = { "secundario" },
                HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 22, 0, 0),
            };
            eliminar.Click += (_, _) => cerrar(new ResultadoEditor(null, Eliminar: true));
            pila.Children.Add(eliminar);
        }

        // las fechas, comprobadas: si alguna está mal, se dice qué le pasa y la ficha sigue abierta
        bool LeerFechas(out Fecha? nac, out Fecha? def, out string? error)
        {
            (nac, var e1) = leerNacimiento();
            (def, var e2) = leerDefuncion();
            var errores = new List<string>();
            if (e1 != null) errores.Add(T("Nacimiento") + ": " + e1);
            if (e2 != null) errores.Add(T("Defunción") + ": " + e2);
            if (nac is { } n && def is { } d && Fecha.Comparar(d, n) < 0) errores.Add(T("La fecha de defunción es anterior a la de nacimiento."));
            error = errores.Count > 0 ? string.Join("\n", errores) : null;
            return error == null;
        }
        async void Guardar()
        {
            if (!LeerFechas(out var nac, out var def, out var error))
            {
                await Principal.Actual.Avisar(T("Revisa las fechas"), error!);
                return;
            }
            p.Nombre = nombre.Text?.Trim() ?? "";
            p.Apellidos = apellidos.Text?.Trim() ?? "";
            p.Sexo = sexo;
            p.Historia = historia.Text?.Trim() ?? "";
            p.Nacimiento = nac;
            p.Defuncion = def;
            cerrar(new ResultadoEditor(p));
        }
        guardar.Click += (_, _) => Guardar();

        bool HayCambios() =>
            (nombre.Text?.Trim() ?? "") != original.Nombre.Trim() || (apellidos.Text?.Trim() ?? "") != original.Apellidos.Trim()
            || sexo != original.Sexo || (historia.Text?.Trim() ?? "") != original.Historia.Trim()
            || p.Foto != original.Foto || p.ArbolEnlazado != original.ArbolEnlazado
            || !LeerFechas(out var nac, out var def, out _) || nac != original.Nacimiento || def != original.Defuncion;
        salir = async () =>
        {
            if (!HayCambios()) { cerrar(new ResultadoEditor(null)); return; }
            string quien = Nombres.SinNombre(original) ? (nueva ? T("esta persona nueva") : T("esta persona")) : original.NombreCompleto;
            int r = await Principal.Actual.Preguntar(T("¿Guardar los cambios?"),
                T("Has cambiado la ficha de {0}.", quien), T("Guardar"), T("Descartar"), T("Seguir editando"));
            if (r == 0) Guardar();
            else if (r == 1) cerrar(new ResultadoEditor(null));
            // «Seguir editando» (o «Atrás» en la pregunta): se queda en la ficha
        };
        if (nueva) nombre.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => nombre.Focus(), Avalonia.Threading.DispatcherPriority.Input);
        return raiz;
    }

    /// <summary>
    /// Una fecha: día, mes y año (solo el año es obligatorio, y nada si no se sabe), con «aproximada» y «antes de Cristo».
    /// Devuelve la fila y cómo leerla: la fecha (o null si está vacía) o lo que le pasa si no es válida.
    /// </summary>
    private static (Control Fila, Func<(Fecha? Fecha, string? Error)> Leer) CampoFecha(Fecha? inicial)
    {
        var dia = new TextBox { Text = inicial?.Dia?.ToString() ?? "", PlaceholderText = T("Día"), MaxLength = 2, HorizontalContentAlignment = HorizontalAlignment.Center };
        var anio = new TextBox { Text = inicial is { } fi ? Math.Abs(fi.Anio).ToString() : "", PlaceholderText = T("Año"), MaxLength = 4, HorizontalContentAlignment = HorizontalAlignment.Center };
        foreach (var t in new[] { dia, anio }) TextInputOptions.SetContentType(t, TextInputContentType.Digits);
        var mes = new ComboBox { PlaceholderText = T("Mes"), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Margin = new Thickness(6, 0) };
        mes.Items.Add("—");
        for (int m = 1; m <= 12; m++) { var n = Core.Idiomas.Fechas.Mes(m); mes.Items.Add(char.ToUpper(n[0]) + n[1..]); }
        mes.SelectedIndex = inicial?.Mes ?? -1;
        var fila = new Grid { ColumnDefinitions = new ColumnDefinitions("0.8*,1.7*,1.1*") };
        fila.Children.Add(dia);
        Grid.SetColumn(mes, 1); fila.Children.Add(mes);
        Grid.SetColumn(anio, 2); fila.Children.Add(anio);
        var aproximada = new CheckBox { Content = T("Aproximada"), IsChecked = inicial?.Aproximada == true, Margin = new Thickness(0, 0, 18, 0) };
        var antes = new CheckBox { Content = T("Antes de Cristo"), IsChecked = inicial?.AntesDeCristo == true };
        var marcas = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        marcas.Children.Add(aproximada); marcas.Children.Add(antes);
        var todo = new StackPanel { Spacing = 4 };
        todo.Children.Add(fila); todo.Children.Add(marcas);

        (Fecha?, string?) Leer()
        {
            string d = dia.Text?.Trim() ?? "", a = anio.Text?.Trim() ?? "";
            int? m = mes.SelectedIndex > 0 ? mes.SelectedIndex : null;
            if (d.Length == 0 && a.Length == 0 && m == null) return (null, null);
            if (a.Length == 0) return (null, T("falta el año."));
            if (!int.TryParse(a, out var y) || y < 1 || y > 9999) return (null, T("el año no es válido."));
            int? dd = null;
            if (d.Length > 0)
            {
                if (!int.TryParse(d, out var x) || x < 1) return (null, T("el día no es válido."));
                if (m == null) return (null, T("con el día, falta el mes."));
                dd = x;
            }
            var f = new Fecha(antes.IsChecked == true ? -y : y, m, dd, aproximada.IsChecked == true);
            return f.EsValida ? (f, null) : (null, T("ese día no existe en ese mes."));
        }
        return (todo, Leer);
    }

    private static TextBlock Etiqueta(string s) => new() { Text = s, FontSize = Ui.L(13), Foreground = Ui.Suave, Margin = new Thickness(2, 6, 0, -6) };

    private static async Task<byte[]?> ElegirImagen(Control desde)
    {
        var almacen = TopLevel.GetTopLevel(desde)?.StorageProvider;
        if (almacen == null) return null;
        var archivos = await almacen.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = T("Elegir foto"), AllowMultiple = false, FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
        });
        if (archivos.Count == 0) return null;
        await using var s = await archivos[0].OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }
}
