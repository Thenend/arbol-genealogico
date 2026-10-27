using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using ArbolGenealogico.Movil.Vistas;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Ordenador;

/// <summary>Los cambios en el árbol: la ficha, añadir y quitar personas, el parentesco, las fotos, fusionar, imprimir y exportar.</summary>
public sealed partial class PantallaPc
{
    // ---------- Cambios, deshacer ----------

    /// <summary>Aplica un cambio al árbol: se puede deshacer, queda pendiente de guardar y el dibujo se recoloca.</summary>
    private void Cambiar(Action<Arbol> cambio)
    {
        if (Doc is not { } d) return;
        var antes = d.Arbol.Clonar();
        cambio(d.Arbol);
        d.Deshacer.Add(antes);
        if (d.Deshacer.Count > 100) d.Deshacer.RemoveAt(0);
        d.Rehacer.Clear();
        d.Modificado = true;
        _lienzo.Refrescar();
        ActualizarFicha();
        ActualizarBarra();
    }

    private void Deshacer(bool rehacer)
    {
        if (Doc is not { } d) return;
        var de = rehacer ? d.Rehacer : d.Deshacer;
        var a = rehacer ? d.Deshacer : d.Rehacer;
        if (de.Count == 0) return;
        a.Add(d.Arbol);
        d.Arbol = de[^1];
        de.RemoveAt(de.Count - 1);
        d.Modificado = true;
        _lienzo.Mostrar(d.Arbol, false);
        ActualizarFicha();
        ActualizarBarra();
        Principal.Actual.Aviso(rehacer ? T("Rehecho") : T("Deshecho"), 1.5);
    }

    // ---------- Ficha, añadir, eliminar ----------

    private async Task Editar(string id, bool nueva)
    {
        if (Doc?.Arbol.Buscar(id) is not { } p) return;
        var r = await EditorPersona.Abrir(p, Doc.Arbol, nueva, ElegirEnlace);
        if (r.Eliminar) { await Eliminar(id); return; }
        if (r.Persona is { } editada)
        {
            Cambiar(a =>
            {
                var q = a.Obtener(id);
                q.Nombre = editada.Nombre; q.Apellidos = editada.Apellidos; q.Sexo = editada.Sexo;
                q.Historia = editada.Historia; q.Foto = editada.Foto; q.ArbolEnlazado = editada.ArbolEnlazado;
                q.Nacimiento = editada.Nacimiento; q.Defuncion = editada.Defuncion; q.Fallecido = editada.Fallecido;
                q.LugarNacimiento = editada.LugarNacimiento;
                if (r.Bodas != null) a.PonerBodas(r.Bodas);
            });
        }
        if (r.CambiarParentesco) { await CambiarParentesco(id); return; }
        if (r.CrearArbol) { await CrearArbolPropio(id); return; }
        if (r.Persona == null && nueva && Doc is { } d)
        {
            // al cancelar la ficha de una persona recién añadida, se quita (como si no se hubiera añadido)
            Deshacer(false);
            d.Rehacer.Clear();
            ActualizarBarra();
        }
    }

    private async Task Eliminar(string id)
    {
        if (Doc is not { } d || d.Arbol.Buscar(id) is not { } p) return;
        if (id == d.Arbol.RaizId)
        {
            await Principal.Actual.Avisar(T("No se puede eliminar"), T("Es la persona principal del árbol: este árbol es el suyo."));
            return;
        }
        var quitadas = d.Arbol.PersonasQueSeEliminarian(id);
        string texto = quitadas.Count <= 1
            ? T("Se quitará del árbol. Se puede deshacer.")
            : N(quitadas.Count - 1, "También se quitará 1 persona que solo estaba unida al árbol a través de ella. Se puede deshacer.",
                "También se quitarán {0} personas que solo estaban unidas al árbol a través de ella. Se puede deshacer.");
        if (await Principal.Actual.Preguntar(T("¿Eliminar a {0}?", Nombres.Mostrar(p)), texto, T("Eliminar"), T("Cancelar")) != 0) return;
        _lienzo.Seleccion = null;
        Cambiar(a => a.Eliminar(id));
    }

    /// <summary>Añade un familiar (si la versión gratuita lo permite) y abre su ficha; si se cancela, no queda.</summary>
    private async Task Anadir(string id, Func<Arbol, string?> anadir)
    {
        if (Doc is not { } d) return;
        if (!Licencia.PuedeAnadirPersonas(d.Arbol) && !await PantallaCompleta.PorPersonas(d.Arbol.Personas.Count)) return;
        string? nuevo = null;
        Cambiar(a => nuevo = anadir(a));
        if (nuevo == null) return;
        _lienzo.Seleccion = nuevo;
        _lienzo.AsegurarVisible(nuevo, _ficha.Bounds.Height + 16);
        await Editar(nuevo, true);
    }

    /// <summary>Los familiares que se pueden añadir a esa persona, para un menú.</summary>
    private List<MenuItem> OpcionesAnadir(string id)
    {
        var lista = new List<MenuItem>();
        if (Doc is not { } d) return lista;
        var a = d.Arbol;
        bool sangre = a.EsSanguinea(id);
        string? politica = sangre ? null : T("Su familia va en su propio árbol");
        MenuItem Item(string texto, Func<Arbol, string?> anadir, bool activo = true, string? ayuda = null)
        {
            var m = new MenuItem { Header = texto, IsEnabled = activo };
            if (ayuda != null) ToolTip.SetTip(m, ayuda);
            m.Click += (_, _) => _ = Anadir(id, anadir);
            return m;
        }
        int padres = a.UnionComoHijo(id)?.Parejas.Count ?? 0;
        if (padres < 2)
            lista.Add(Item(padres == 1 ? T("El padre o la madre que falta") : T("Padres"), x => x.AnadirPadres(id).FirstOrDefault()?.Id, sangre, politica));
        lista.Add(Item(T("Hermano o hermana"), x => x.AnadirHermano(id).hermano.Id, sangre, politica));
        lista.Add(Item(T("Pareja"), x => x.AnadirPareja(id).Id));
        var uniones = a.UnionesComoPareja(id).ToList();
        if (uniones.Count <= 1) lista.Add(Item(T("Hijo o hija"), x => x.AnadirHijo(id, null).Id));
        else
        {
            var hijo = new MenuItem { Header = T("Hijo o hija") };
            foreach (var u in uniones)
            {
                var otro = u.Parejas.FirstOrDefault(x => x != id);
                var uid = u.Id;
                hijo.Items.Add(Item(T("Con {0}", otro == null ? T("pareja desconocida") : Nombres.Mostrar(a.Buscar(otro))), x => x.AnadirHijo(id, uid).Id));
            }
            lista.Add(hijo);
        }
        return lista;
    }

    /// <summary>El menú de añadir familiar, junto al botón que lo abre.</summary>
    private void MenuAnadir(string id, Control junto)
    {
        var menu = new MenuFlyout();
        foreach (var m in OpcionesAnadir(id)) menu.Items.Add(m);
        menu.Placement = PlacementMode.TopEdgeAlignedLeft;
        menu.ShowAt(junto);
    }

    /// <summary>
    /// El menú de una persona (botón derecho, Insert o el «+» de su tarjeta): añadir familiares, su ficha, su árbol, el
    /// parentesco y eliminarla. Sobre el fondo, ver todo y la persona principal.
    /// </summary>
    private void MostrarMenuPersona(string? id, Point? donde)
    {
        if (Doc is not { } d) return;
        var menu = new MenuFlyout();
        MenuItem Item(string texto, Action accion, string? tecla = null, bool activo = true)
        {
            // la tecla a la derecha, más clara (la de Avalonia se sale del menú cuando hay opciones sin tecla)
            object cabecera = texto;
            if (tecla != null)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), HorizontalAlignment = HorizontalAlignment.Stretch };
                g.Children.Add(new TextBlock { Text = texto });
                var t = new TextBlock { Text = tecla, Foreground = Ui.Suave, Margin = new Thickness(28, 0, 0, 0) };
                Grid.SetColumn(t, 1);
                g.Children.Add(t);
                cabecera = g;
            }
            var m = new MenuItem { Header = cabecera, IsEnabled = activo };
            m.Click += (_, _) => accion();
            return m;
        }
        if (id == null || d.Arbol.Buscar(id) is not { } p)
        {
            menu.Items.Add(Item(T("Ver todo el árbol"), () => _lienzo.Encuadrar(), "Ctrl+0"));
            menu.Items.Add(Item(T("Ir a la persona principal"), IrAPrincipal, T("Inicio")));
        }
        else
        {
            var titulo = new MenuItem { Header = T("Añadir familiar de {0}", Nombres.Mostrar(p)), IsEnabled = false };
            menu.Items.Add(titulo);
            foreach (var m in OpcionesAnadir(id)) menu.Items.Add(m);
            menu.Items.Add(new Separator());
            menu.Items.Add(Item(T("Ficha") + "…", () => _ = Editar(id, false), T("Intro")));
            if (id != d.Arbol.RaizId)
            {
                if (!string.IsNullOrEmpty(p.ArbolEnlazado)) menu.Items.Add(Item(T("Su árbol"), () => _ = AbrirEnlace(id), T("Ctrl+Intro")));
                else menu.Items.Add(Item(T("Crear un árbol nuevo a partir de esta persona") + "…", () => _ = CrearArbolPropioConLicencia(id)));
            }
            if (d.Arbol.PorQueNoSeMueve(id) == null) menu.Items.Add(Item(T("Cambiar parentesco") + "…", () => _ = CambiarParentesco(id)));
            menu.Items.Add(Item(T("Poner una foto…"), () => _ = ElegirFoto(id)));
            if (id != d.Arbol.RaizId)
            {
                menu.Items.Add(new Separator());
                menu.Items.Add(Item(T("Eliminar") + "…", () => _ = Eliminar(id), T("Supr")));
            }
        }
        if (donde != null)
        {
            menu.Placement = PlacementMode.Pointer;
            menu.ShowAt(_lienzo, true);
            return;
        }
        // con el teclado o el «+» de la tarjeta: debajo de la tarjeta (o en medio, sobre el fondo)
        var junto = id != null && _lienzo.RectDe(id) is { } r && _lienzo.Centro(id) is { } c
            ? new Point(c.X - r.Width * _lienzo.Escala / 2, c.Y + r.Height * _lienzo.Escala / 2 + 4)
            : new Point(_lienzo.Bounds.Width / 2, _lienzo.Bounds.Height / 2);
        menu.Placement = PlacementMode.AnchorAndGravity;
        menu.PlacementAnchor = Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.TopLeft;
        menu.PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.BottomRight;
        menu.HorizontalOffset = Math.Clamp(junto.X, 0, _lienzo.Bounds.Width);
        menu.VerticalOffset = Math.Clamp(junto.Y, 0, _lienzo.Bounds.Height);
        menu.ShowAt(_lienzo);
    }

    private async Task CrearArbolPropioConLicencia(string id)
    {
        if (!Licencia.Completa && !await PantallaCompleta.PorEnlace()) return;
        await CrearArbolPropio(id);
    }

    // ---------- Cambiar el parentesco: se pulsa a la otra persona en el árbol ----------

    private TaskCompletionSource<string?>? _eligiendo;
    private string? _eligiendoPara;

    private Task<string?> ElegirPersona(string texto, string para)
    {
        _eligiendo = new TaskCompletionSource<string?>();
        _eligiendoPara = para;
        _textoEleccion.Text = texto;
        _eleccion.IsVisible = true;
        _lienzo.Seleccion = null;
        _lienzo.ResaltarCoincidencias(new[] { para });
        ActualizarFicha();
        return _eligiendo.Task;
    }

    private void TerminarEleccion(string? id)
    {
        if (_eligiendo is not { } eleccion) return;
        if (id != null && id == _eligiendoPara)
        {
            Principal.Actual.Aviso(T("Elige a otra persona"), 1.8);
            _lienzo.Seleccion = null;
            return;
        }
        _eligiendo = null;
        _eleccion.IsVisible = false;
        _lienzo.ResaltarCoincidencias(Array.Empty<string>());
        _lienzo.Seleccion = _eligiendoPara;
        eleccion.SetResult(id);
    }

    private async Task CambiarParentesco(string id)
    {
        if (Doc is not { } d || d.Arbol.Buscar(id) is not { } p) return;
        if (!await CambioParentesco.Hacer(d.Arbol, id, () => ElegirPersona(T("Haz clic en la persona de la que es familiar {0}", Nombres.Mostrar(p)), id), Cambiar)) return;
        _lienzo.Seleccion = id;
        _lienzo.CentrarEn(id, null, true);
    }

    // ---------- Fotos: arrastradas, pegadas o elegidas ----------

    private static readonly string[] ExtensionesImagen = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic" };

    private static bool EsImagen(string nombre) => ExtensionesImagen.Contains(Path.GetExtension(nombre).ToLowerInvariant());

    private void AlArrastrar(object? s, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        var archivos = e.DataTransfer.TryGetFiles();
        bool imagen = archivos?.Any(f => EsImagen(f.Name)) == true || e.DataTransfer.TryGetBitmap() != null;
        if (imagen && Doc != null)
        {
            var id = _lienzo.TarjetaEnPunto(e.GetPosition(_lienzo));
            _lienzo.Destino = id;
            if (id != null) e.DragEffects = DragDropEffects.Copy;
        }
        else if (archivos?.Any(f => Principal.EsArbol(f.Name) || Gedcom.EsGedcom(f.Name)) == true) e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void AlSoltar(object? s, DragEventArgs e)
    {
        var id = _lienzo.Destino;
        _lienzo.Destino = null;
        e.Handled = true;
        var archivos = e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>();
        try
        {
            if (archivos.FirstOrDefault(f => EsImagen(f.Name)) is IStorageFile foto && id != null)
            {
                await using var st = await foto.OpenReadAsync();
                using var ms = new MemoryStream();
                await st.CopyToAsync(ms);
                await PonerFoto(id, ms.ToArray());
            }
            else if (e.DataTransfer.TryGetBitmap() is { } bmp && id != null) await PonerFoto(id, Bytes(bmp));
            else if (archivos.FirstOrDefault(f => Principal.EsArbol(f.Name) || Gedcom.EsGedcom(f.Name)) is IStorageFile arbol && arbol.TryGetLocalPath() is { } ruta)
                await AbrirRecibido(new ArchivoRecibido(arbol.Name, await File.ReadAllBytesAsync(ruta), ruta));
        }
        catch (Exception ex) { await Principal.Actual.Error(T("No se pudo completar"), ex); }
    }

    private static byte[] Bytes(Avalonia.Media.Imaging.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Ctrl+V: la imagen (o el archivo de imagen) del portapapeles, como foto de la persona elegida.</summary>
    private async Task PegarFoto()
    {
        if (_lienzo.Seleccion is not { } id || TopLevel.GetTopLevel(this)?.Clipboard is not { } portapapeles) return;
        try
        {
            if (await portapapeles.TryGetFilesAsync() is { } archivos && archivos.FirstOrDefault(f => EsImagen(f.Name)) is IStorageFile foto)
            {
                await using var st = await foto.OpenReadAsync();
                using var ms = new MemoryStream();
                await st.CopyToAsync(ms);
                await PonerFoto(id, ms.ToArray());
            }
            else if (await portapapeles.TryGetBitmapAsync() is { } bmp) await PonerFoto(id, Bytes(bmp));
        }
        catch (Exception ex) { await Principal.Actual.Error(T("No se pudo completar"), ex); }
    }

    private async Task ElegirFoto(string id)
    {
        if (Almacen is not { } almacen) return;
        var archivos = await almacen.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = T("Poner una foto…"), FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
            SuggestedStartLocation = await almacen.TryGetWellKnownFolderAsync(WellKnownFolder.Pictures),
        });
        if (archivos.Count == 0) return;
        await using var st = await archivos[0].OpenReadAsync();
        using var ms = new MemoryStream();
        await st.CopyToAsync(ms);
        await PonerFoto(id, ms.ToArray());
    }

    /// <summary>La foto se encuadra en el círculo (como en la ficha) y se pone a la persona.</summary>
    private async Task PonerFoto(string id, byte[] datos)
    {
        if (Fotos.Abrir(datos) is not { } img)
        {
            await Principal.Actual.Avisar(T("No se pudo completar"), T("No es una imagen que se pueda abrir."));
            return;
        }
        if (await EncuadrarFoto.Abrir(img) is not { } recorte) return;
        var foto = Fotos.Recortar(img, recorte);
        Cambiar(a => a.Obtener(id).Foto = foto);
        _lienzo.Seleccion = id;
    }

    // ---------- Fusionar, imprimir, exportar ----------

    private async Task FusionarCon()
    {
        if (Doc == null || await ElegirArchivoArbol(T("¿Qué árbol añades a este?")) is not { } ruta) return;
        try
        {
            var datos = await File.ReadAllBytesAsync(ruta);
            var nombre = Path.GetFileNameWithoutExtension(ruta);
            var otro = Gedcom.EsGedcom(ruta) || Gedcom.PareceGedcom(datos)
                ? Gedcom.Importar(datos, Path.GetFileName(ruta), out _)
                : ArbolJson.Cargar(ruta, out _);
            await Fusionar(otro, nombre);
        }
        catch (Exception ex) { await Principal.Actual.Error(T("No se pudo abrir el árbol"), ex); }
    }

    private async Task Fusionar(Arbol otro, string nombreOtro)
    {
        if (Doc is not { } d) return;
        var copia = d.Arbol.Clonar();
        var (inf, _) = await PantallaFusion.Preparar(copia, otro, nombreOtro);
        if (inf == null) return;
        Cambiar(a =>
        {
            // el árbol fusionado pasa a ser el de ahora (lo de antes queda en deshacer)
            a.Personas.Clear(); a.Personas.AddRange(copia.Personas);
            a.Uniones.Clear(); a.Uniones.AddRange(copia.Uniones);
        });
        PantallaFusion.Avisar(inf);
    }

    private Task Imprimir() => Doc is { } d
        ? PantallaImpresion.Abrir(d.Arbol, _lienzo.Forma, _lienzo.Ordenacion, _lienzo.MarcaHistoria, d.Titulo)
        : Task.CompletedTask;

    private void MenuExportar(Control? junto)
    {
        if (Doc == null) return;
        var menu = new MenuFlyout();
        MenuItem Item(string texto, string detalle, Func<Task> accion)
        {
            var m = new MenuItem { Header = new StackPanel { Children = { new TextBlock { Text = texto }, new TextBlock { Text = detalle, FontSize = 12, Foreground = Ui.Suave, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 380 } } } };
            m.Click += async (_, _) =>
            {
                try { await accion(); }
                catch (Exception ex) { await Principal.Actual.Error(T("No se pudo completar"), ex); }
            };
            return m;
        }
        menu.Items.Add(Item(T("Guardar como PDF…"), T("Se abre sin esta aplicación, con la máxima calidad aunque el árbol sea grande, y permite buscar por nombre"), ExportarPdf));
        menu.Items.Add(Item(T("Guardar como imagen…"), T("Una imagen PNG del árbol entero, para árboles pequeños"), ExportarImagen));
        menu.Items.Add(Item(T("Exportar a GEDCOM…"), T("Para otros programas de genealogía (Ancestry, MyHeritage, FamilySearch…). Sin fotos"), ExportarGedcom));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(T("Guardar como…"), T("Una copia del árbol (.ramo) con otro nombre o en otra carpeta"), () => Guardar(Doc, true)));
        menu.ShowAt(junto ?? _lienzo, junto == null);
    }

    private Task<string?> DondeGuardar(string extension, string tipo, string? sufijo = null) =>
        Doc is { } d ? Exportar.Donde(d.Titulo + (sufijo ?? ""), extension, tipo, d.Ruta) : Task.FromResult<string?>(null);

    private static Task Exportado(string ruta) => Exportar.Hecho(ruta);

    private async Task ExportarPdf()
    {
        if (await DondeGuardar(".pdf", "PDF") is not { } ruta) return;
        Principal.Actual.Aviso(T("Preparando el PDF…"));
        await Task.Yield();
        await _lienzo.GuardarPdf(ruta, Doc!.Titulo, Licencia.ConMarca);
        await Exportado(ruta);
    }

    private async Task ExportarImagen()
    {
        if (await DondeGuardar(".png", T("Imagen PNG")) is not { } ruta) return;
        Principal.Actual.Aviso(T("Preparando la imagen…"));
        await Task.Yield();
        using (var img = _lienzo.Imagen(2, Licencia.ConMarca)) img.Save(ruta);
        await Exportado(ruta);
    }

    private async Task ExportarGedcom()
    {
        if (Doc is not { } d || await DondeGuardar(Gedcom.Extension, "GEDCOM") is not { } ruta) return;
        await File.WriteAllBytesAsync(ruta, Gedcom.Exportar(d.Arbol, Path.GetFileName(ruta)));
        await Exportado(ruta);
    }
}
