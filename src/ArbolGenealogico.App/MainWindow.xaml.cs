using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using Microsoft.Win32;

namespace ArbolGenealogico.App;

public partial class MainWindow : Window
{
    private readonly List<Documento> _pila = new();
    private readonly Preferencias _prefs = Preferencias.Cargar();
    private List<string> _coincidencias = new();
    private int _indiceCoincidencia = -1;
    private Documento Doc => _pila[^1];
    private Arbol Arbol => Doc.Arbol;

    public MainWindow(string? archivoInicial)
    {
        InitializeComponent();
        Dwm.Aplicar(this);
        Mini.Vista = Vista;
        if (_prefs.Ordenacion == "A") OrdenacionA.IsChecked = true;      // por defecto, Balanceado
        else if (_prefs.Ordenacion == "B") OrdenacionB.IsChecked = true;
        else if (_prefs.Ordenacion == "D") OrdenacionD.IsChecked = true;
        else if (_prefs.Ordenacion == "E") OrdenacionE.IsChecked = true;
        // la forma de las tarjetas (de versiones anteriores puede venir solo "estrechas sí o no")
        PonerTarjetas(_prefs.Tarjetas != null ? FormaTarjeta.Leer(_prefs.Tarjetas) : _prefs.TarjetasEstrechas ? FormaTarjeta.Leer("estrechas") : FormaTarjeta.Ancha);
        CargarGuia();
        Vista.GuiaCambiada += MostrarGuia;
        GuiaPanel.SizeChanged += (_, _) => Vista.MargenSuperior = Vista.GuiaVisible ? GuiaPanel.ActualHeight + GuiaPanel.Margin.Top : 0;
        RestaurarVentana();

        Vista.SeleccionCambiada += _ => ActualizarCabecera();
        Vista.EditarSolicitado += id => EditarPersona(id, false);
        Vista.MenuSolicitado += MostrarMenu;
        Vista.AbrirEnlaceSolicitado += AbrirEnlace;
        Vista.CamaraCambiada += () => AyudaTxt.Opacity = Vista.Escala < 0.5 ? 0.0 : 1.0;

        PreviewKeyDown += AlTeclear;
        Drop += AlSoltarArchivo;
        DragOver += AlArrastrarSobre;
        DragLeave += (_, _) => Vista.ResaltarDestino(null);

        var abierto = false;
        foreach (var ruta in new[] { archivoInicial, _prefs.UltimoArchivo })
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) continue;
            if (CargarArchivo(ruta, silencioso: ruta == _prefs.UltimoArchivo, reemplazar: true)) { abierto = true; break; }
        }
        if (!abierto) AbrirDocumentoNuevo();
    }

    /// <summary>La ventana se abre donde y como estaba al cerrarla (si ese sitio sigue estando en alguna pantalla).</summary>
    private void RestaurarVentana()
    {
        if (_prefs.Ventana is not { } v || v.Ancho < MinWidth || v.Alto < MinHeight) return;
        var pantalla = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var r = new Rect(v.Izquierda, v.Arriba, v.Ancho, v.Alto);
        if (!pantalla.IntersectsWith(r) || Rect.Intersect(pantalla, r).Width < 200 || Rect.Intersect(pantalla, r).Height < 120) return;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = r.X; Top = r.Y; Width = r.Width; Height = r.Height;
        if (v.Maximizada) WindowState = WindowState.Maximized;
    }

    /// <summary>Guarda la vista del árbol que se está viendo, para recuperarla la próxima vez que se abra.</summary>
    private void RecordarVista()
    {
        if (_pila.Count == 0) return;
        _prefs.RecordarVista(Doc.Ruta, Vista.VistaActual());
    }

    // ---------- Documentos ----------
    private void MostrarDocumento(bool ajustar)
    {
        Vista.SeleccionId = null;
        var guardada = ajustar ? _prefs.VistaDe(Doc.Ruta) : null;
        Vista.VistaPendiente = guardada;
        Vista.Cargar(Arbol, ajustar);
        BuscarBox.Clear();
        if (guardada?.Seleccion is { } sel && Arbol.Buscar(sel) != null) Vista.SeleccionId = sel;
        ActualizarCabecera();
    }

    private void AbrirDocumentoNuevo()
    {
        RecordarVista();
        _pila.Clear();
        _pila.Add(new Documento(Arbol.Nuevo("Mi familia")));
        MostrarDocumento(true);
        Vista.SeleccionId = Arbol.RaizId;
        Vista.CentrarEn(Arbol.RaizId, 1, false);
    }

    private bool CargarArchivo(string ruta, bool silencioso = false, bool reemplazar = false)
    {
        try
        {
            var arbol = ArbolJson.Cargar(ruta, out var avisos);
            if (arbol.Personas.Count == 0) throw new InvalidDataException("El archivo no contiene personas.");
            var doc = new Documento(arbol, Path.GetFullPath(ruta));
            RecordarVista();
            if (reemplazar) _pila.Clear();
            _pila.Add(doc);
            MostrarDocumento(true);
            if (_pila.Count == 1) { _prefs.UltimoArchivo = doc.Ruta; _prefs.Guardar(); }
            if (avisos.Count > 0 && !silencioso)
                DialogoMensaje.Avisar(this, "Archivo abierto con avisos", string.Join("\n", avisos.Distinct()));
            return true;
        }
        catch (Exception ex)
        {
            if (!silencioso) DialogoMensaje.Avisar(this, "No se pudo abrir el archivo", ex.Message);
            return false;
        }
    }

    private void ActualizarCabecera()
    {
        if (_pila.Count == 0) return;
        TituloTxt.Text = Doc.Titulo + (Doc.Modificado ? "  ●" : "");
        var persona = Vista.SeleccionId != null ? Arbol.Buscar(Vista.SeleccionId) : null;
        var resto = $"{Arbol.Personas.Count} persona{(Arbol.Personas.Count == 1 ? "" : "s")}" + (Doc.Ruta == null ? " · sin guardar" : "");
        MigasTxt.Text = _pila.Count > 1 ? string.Join("  ›  ", _pila.Select(d => d.Titulo)) + "   ·   " + resto : resto;
        Title = $"{Doc.Titulo}{(Doc.Modificado ? " *" : "")} — Árbol genealógico";
        AtrasBtn.Visibility = _pila.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        DeshacerBtn.IsEnabled = Doc.PuedeDeshacer;
        RehacerBtn.IsEnabled = Doc.PuedeRehacer;
        GuardarBtn.IsEnabled = Doc.Modificado || Doc.Ruta == null;
        RecalcularBusqueda();
    }

    private bool GuardarDoc(Documento d, bool como)
    {
        var ruta = d.Ruta;
        if (como || ruta == null)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Guardar árbol", Filter = "Árbol genealógico (*.json)|*.json", DefaultExt = ".json", AddExtension = true,
                FileName = Limpiar(d.Titulo) + ".json",
            };
            if (d.Ruta != null) dlg.InitialDirectory = Path.GetDirectoryName(d.Ruta);
            else if (_pila.Count > 1 || _prefs.UltimoArchivo != null) dlg.InitialDirectory = Path.GetDirectoryName(_prefs.UltimoArchivo ?? "");
            if (dlg.ShowDialog(this) != true) return false;
            ruta = Path.GetFullPath(dlg.FileName);
            if (d.Ruta != null && !string.Equals(Path.GetDirectoryName(d.Ruta), Path.GetDirectoryName(ruta), StringComparison.OrdinalIgnoreCase))
                RebasarEnlaces(d.Arbol, d.Ruta, ruta);
        }
        try
        {
            d.Arbol.Nombre = Path.GetFileNameWithoutExtension(ruta);   // el nombre guardado dentro sigue al del archivo
            ArbolJson.Guardar(ruta, d.Arbol);
            d.Ruta = ruta; d.Modificado = false;
            if (_pila.Count > 0 && ReferenceEquals(_pila[0], d)) { _prefs.UltimoArchivo = ruta; _prefs.Guardar(); }
            ActualizarCabecera();
            return true;
        }
        catch (Exception ex)
        {
            DialogoMensaje.Avisar(this, "No se pudo guardar", ex.Message);
            return false;
        }
    }

    private static string Limpiar(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrWhiteSpace(s) ? "arbol" : s.Trim();
    }

    private static void RebasarEnlaces(Arbol a, string rutaVieja, string rutaNueva)
    {
        foreach (var p in a.Personas.Where(p => !string.IsNullOrEmpty(p.ArbolEnlazado) && !Path.IsPathRooted(p.ArbolEnlazado)))
            p.ArbolEnlazado = ArbolJson.RutaRelativa(rutaNueva, ArbolJson.ResolverRuta(rutaVieja, p.ArbolEnlazado!));
    }

    /// <summary>Pregunta por los cambios sin guardar de todos los árboles abiertos. false = cancelar.</summary>
    private bool ConfirmarCerrarTodo()
    {
        foreach (var d in _pila.Where(d => d.Modificado).ToList())
        {
            int r = DialogoMensaje.Preguntar(this, "Cambios sin guardar", $"«{d.Titulo}» tiene cambios sin guardar. ¿Quieres guardarlos?",
                new[] { "Guardar", "No guardar", "Cancelar" }, 0, 2);
            if (r == 0) { if (!GuardarDoc(d, false)) return false; }
            else if (r != 1) return false;
        }
        return true;
    }

    /// <summary>
    /// Pregunta si guardar los cambios de un árbol antes de pasar a otro, igual que al abrir uno con «Abrir».
    /// false = cancelar (o no se pudo guardar). Si no tiene cambios, no pregunta.
    /// </summary>
    private bool PreguntarGuardar(Documento d, string antesDe)
    {
        if (!d.Modificado) return true;
        int r = DialogoMensaje.Preguntar(this, "Cambios sin guardar", $"«{d.Titulo}» tiene cambios sin guardar. ¿Quieres guardarlos {antesDe}?",
            new[] { "Guardar", "No guardar", "Cancelar" }, 0, 2);
        if (r == 0) return GuardarDoc(d, false);
        return r == 1;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmarCerrarTodo()) { e.Cancel = true; return; }
        // La próxima vez se abrirá igual: ventana, árbol y vista.
        RecordarVista();
        var r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!r.IsEmpty)
            _prefs.Ventana = new PosicionVentana { Izquierda = r.X, Arriba = r.Y, Ancho = r.Width, Alto = r.Height, Maximizada = WindowState == WindowState.Maximized };
        _prefs.Guardar();
        base.OnClosing(e);
    }

    // ---------- Barra superior ----------
    private void Nuevo_Click(object s, RoutedEventArgs e) { if (ConfirmarCerrarTodo()) { AbrirDocumentoNuevo(); EditarPersona(Arbol.RaizId, false); } }

    private void Abrir_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Abrir árbol", Filter = "Árbol genealógico (*.json)|*.json|Todos|*.*" };
        if (Doc.Ruta != null) dlg.InitialDirectory = Path.GetDirectoryName(Doc.Ruta);
        if (dlg.ShowDialog(this) != true || !ConfirmarCerrarTodo()) return;
        CargarArchivo(dlg.FileName, reemplazar: true);
    }

    private void Guardar_Click(object s, RoutedEventArgs e) => GuardarDoc(Doc, false);
    private void GuardarComo_Click(object s, RoutedEventArgs e) => GuardarDoc(Doc, true);
    private void Deshacer_Click(object s, RoutedEventArgs e) => Deshacer();
    private void Rehacer_Click(object s, RoutedEventArgs e) => Rehacer();
    private void CentrarPrincipal_Click(object s, RoutedEventArgs e) { Vista.CentrarEn(Arbol.RaizId, 1.0); Vista.SeleccionId = Arbol.RaizId; }
    private void Acercar_Click(object s, RoutedEventArgs e) => Vista.Zoom(1.3);
    private void Alejar_Click(object s, RoutedEventArgs e) => Vista.Zoom(1 / 1.3);
    private void Ajustar_Click(object s, RoutedEventArgs e) => Vista.Ajustar();
    private void Atras_Click(object s, RoutedEventArgs e) => Atras();

    private void Deshacer()
    {
        if (!Doc.Deshacer()) return;
        Vista.Cargar(Arbol, false); ActualizarCabecera();
    }

    private void Rehacer()
    {
        if (!Doc.Rehacer()) return;
        Vista.Cargar(Arbol, false); ActualizarCabecera();
    }

    private void Atras()
    {
        if (_pila.Count < 2) return;
        if (!PreguntarGuardar(Doc, "antes de volver")) return;
        RecordarVista();
        _pila.RemoveAt(_pila.Count - 1);
        MostrarDocumento(true);     // vuelve a la vista que tenía al salir de él
    }

    /// <summary>Arrastrar una imagen sobre una tarjeta ilumina la tarjeta; arrastrar un .json lo abrirá.</summary>
    private void AlArrastrarSobre(object s, DragEventArgs e)
    {
        e.Handled = true;
        if (Fotos.DatosTienenImagen(e.Data))
        {
            var id = Vista.PersonaEn(e.GetPosition(Vista));
            Vista.ResaltarDestino(id);
            e.Effects = id != null ? DragDropEffects.Copy : DragDropEffects.None;
            return;
        }
        Vista.ResaltarDestino(null);
        bool hayJson = e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] fs
            && fs.Any(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        e.Effects = hayJson ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void AlSoltarArchivo(object s, DragEventArgs e)
    {
        Vista.ResaltarDestino(null);
        if (Fotos.DatosTienenImagen(e.Data))
        {
            var id = Vista.PersonaEn(e.GetPosition(Vista));
            if (id == null) return;
            try
            {
                var foto = Fotos.DesdeDatos(e.Data);
                if (foto != null) AsignarFoto(id, foto);
            }
            catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo cargar la foto", ex.Message); }
            return;
        }
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        var ruta = files.FirstOrDefault(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (ruta != null && ConfirmarCerrarTodo()) CargarArchivo(ruta, reemplazar: true);
    }

    /// <summary>Pone la foto a la persona (con deshacer) y la deja seleccionada.</summary>
    private void AsignarFoto(string id, string fotoBase64)
    {
        var p = Arbol.Buscar(id);
        if (p == null) return;
        Doc.Registrar();
        p.Foto = fotoBase64;
        Vista.Refrescar(false);
        Vista.SeleccionId = id;
        ActualizarCabecera();
    }

    /// <summary>Ctrl+V: la imagen (o archivo de imagen) del portapapeles pasa a ser la foto de la persona seleccionada.</summary>
    private void PegarFoto()
    {
        var id = Vista.SeleccionId;
        if (id == null)
        {
            DialogoMensaje.Avisar(this, "Pegar foto", "Selecciona primero a la persona a la que quieres ponerle la foto.");
            return;
        }
        try
        {
            var foto = Fotos.DesdePortapapeles();
            if (foto != null) AsignarFoto(id, foto);
        }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo pegar la foto", ex.Message); }
    }

    private void AlTeclear(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), mayus = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool flecha = key is Key.Left or Key.Right or Key.Up or Key.Down;

        // Mientras se escribe en el buscador, las teclas son del cuadro de texto (salvo estos atajos).
        if (e.OriginalSource is System.Windows.Controls.TextBox && !(ctrl && key is Key.S or Key.O or Key.N or Key.F)) return;

        if (ctrl && key == Key.S) { GuardarDoc(Doc, mayus); e.Handled = true; }
        else if (ctrl && key == Key.O) { Abrir_Click(this, e); e.Handled = true; }
        else if (ctrl && key == Key.N) { Nuevo_Click(this, e); e.Handled = true; }
        else if (ctrl && key == Key.Z) { if (mayus) Rehacer(); else Deshacer(); e.Handled = true; }
        else if (ctrl && key == Key.Y) { Rehacer(); e.Handled = true; }
        else if (ctrl && key == Key.V && Fotos.PortapapelesTieneImagen()) { PegarFoto(); e.Handled = true; }
        else if (ctrl && (key == Key.D0 || key == Key.NumPad0)) { Vista.Ajustar(); e.Handled = true; }
        else if (ctrl && key is Key.Add or Key.OemPlus) { Vista.Zoom(1.3); e.Handled = true; }
        else if (ctrl && key is Key.Subtract or Key.OemMinus) { Vista.Zoom(1 / 1.3); e.Handled = true; }
        else if (key == Key.Home) { CentrarPrincipal_Click(this, e); e.Handled = true; }
        else if (key == Key.Left && alt) { Atras(); e.Handled = true; }
        else if (key == Key.F1) { MostrarAtajos(); e.Handled = true; }
        else if (ctrl && mayus && key == Key.F) { PonerTarjetas(Vista.Tarjetas with { Foto = !Vista.Tarjetas.Foto }); e.Handled = true; }
        else if (ctrl && key == Key.F) { EnfocarBusqueda(); e.Handled = true; }
        else if (ctrl && key == Key.T) { PonerTarjetas(Vista.Tarjetas with { Vertical = !Vista.Tarjetas.Vertical }); e.Handled = true; }
        else if (ctrl && key == Key.G) { PonerTarjetas(Vista.Tarjetas with { LetraGrande = !Vista.Tarjetas.LetraGrande }); e.Handled = true; }
        else if (ctrl && key == Key.H) { Guia_Click(this, e); e.Handled = true; }
        else if (ctrl && key == Key.P) { Imprimir_Click(this, e); e.Handled = true; }
        else if (ctrl && key == Key.L)
        {
            (Vista.Ordenacion switch { Ordenacion.A => OrdenacionB, Ordenacion.B => OrdenacionC, Ordenacion.C => OrdenacionD, Ordenacion.D => OrdenacionE, _ => OrdenacionA }).IsChecked = true;
            e.Handled = true;
        }
        else if (key == Key.F3) { SiguienteCoincidencia(mayus ? -1 : 1); e.Handled = true; }
        else if (flecha && ctrl)
        {
            const double paso = 140;
            Vista.Desplazar(key == Key.Left ? paso : key == Key.Right ? -paso : 0, key == Key.Up ? paso : key == Key.Down ? -paso : 0);
            e.Handled = true;
        }
        else if (flecha && !alt) { Navegar(key switch { Key.Left => Direccion.Izquierda, Key.Right => Direccion.Derecha, Key.Up => Direccion.Arriba, _ => Direccion.Abajo }); e.Handled = true; }
        else if (key is Key.Insert or Key.Add or Key.OemPlus or Key.Apps || (key == Key.F10 && mayus)) { AbrirMenuSeleccion(); e.Handled = true; }
        else if (key == Key.Escape && Vista.SeleccionId != null) { Vista.SeleccionId = null; e.Handled = true; }
        else if (key == Key.Escape && BuscarBox.Text.Length > 0) { BuscarBox.Clear(); e.Handled = true; }
        else if (Vista.SeleccionId is { } id)
        {
            if (ctrl && key is Key.Enter or Key.Return) { AbrirEnlace(id); e.Handled = true; }
            else if (key is Key.F2 or Key.Enter or Key.Return) { EditarPersona(id, false); e.Handled = true; }
            else if (key == Key.Delete) { EliminarPersona(id); e.Handled = true; }
        }
    }

    // ---------- Búsqueda ----------
    private void EnfocarBusqueda()
    {
        BuscarBox.Focus();
        BuscarBox.SelectAll();
    }

    private void BuscarBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _indiceCoincidencia = -1;
        RecalcularBusqueda();
    }

    private void BuscarBox_FocoCambia(object sender, KeyboardFocusChangedEventArgs e) =>
        BusquedaBorde.BorderBrush = BuscarBox.IsKeyboardFocused
            ? (System.Windows.Media.Brush)FindResource("AcentoBrush")
            : (System.Windows.Media.Brush)FindResource("BordeBrush");

    private void BuscarLimpiar_Click(object sender, RoutedEventArgs e)
    {
        BuscarBox.Clear();
        BuscarBox.Focus();
    }

    private void BuscarBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            SiguienteCoincidencia(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            BuscarBox.Clear();
            Keyboard.Focus(Vista);
            Vista.Focus();
            e.Handled = true;
        }
    }

    /// <summary>Actualiza la lista de coincidencias, el contador y los anillos de las tarjetas.</summary>
    private void RecalcularBusqueda()
    {
        if (_pila.Count == 0) return;
        string texto = BuscarBox.Text;
        BuscarPlaceholder.Visibility = texto.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        BuscarLimpiar.Visibility = texto.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        string? actual = _indiceCoincidencia >= 0 && _indiceCoincidencia < _coincidencias.Count ? _coincidencias[_indiceCoincidencia] : null;
        var encontradas = Arbol.BuscarPorNombre(texto).Select(p => p.Id);
        _coincidencias = Vista.OrdenDeLectura(encontradas);
        _indiceCoincidencia = actual == null ? -1 : _coincidencias.IndexOf(actual);
        Vista.ResaltarCoincidencias(_coincidencias);

        var normal = (System.Windows.Media.Brush)FindResource("TextoSuaveBrush");
        if (texto.Trim().Length == 0) BuscarContador.Text = "";
        else if (_coincidencias.Count == 0) { BuscarContador.Text = "Sin coincidencias"; BuscarContador.Foreground = (System.Windows.Media.Brush)FindResource("PeligroBrush"); return; }
        else if (_indiceCoincidencia >= 0) BuscarContador.Text = $"{_indiceCoincidencia + 1} de {_coincidencias.Count}";
        else BuscarContador.Text = _coincidencias.Count == 1 ? "1 coincidencia" : $"{_coincidencias.Count} coincidencias";
        BuscarContador.Foreground = normal;
    }

    /// <summary>Pasa a la siguiente (o anterior) coincidencia: la selecciona y mueve la vista hasta ella.</summary>
    private void SiguienteCoincidencia(int paso)
    {
        int n = _coincidencias.Count;
        if (n == 0) return;
        _indiceCoincidencia = _indiceCoincidencia < 0 ? (paso > 0 ? 0 : n - 1) : (_indiceCoincidencia + paso + n) % n;
        var id = _coincidencias[_indiceCoincidencia];
        Vista.SeleccionId = id;
        Vista.CentrarEn(id, Math.Max(Vista.Escala, 0.7));
        RecalcularBusqueda();
    }

    /// <summary>Mueve la selección con las flechas (la primera pulsación, sin selección, elige a la persona principal).</summary>
    private void Navegar(Direccion direccion)
    {
        var actual = Vista.SeleccionId;
        var destino = actual == null ? Arbol.RaizId : Vista.Vecino(actual, direccion);
        if (destino == null) return;
        Vista.SeleccionId = destino;
        Vista.MostrarPersona(destino, true);
    }

    /// <summary>Abre el menú de añadir familiares de la persona seleccionada (equivale al botón «+»).</summary>
    private void AbrirMenuSeleccion()
    {
        var id = Vista.SeleccionId;
        if (id == null) { id = Arbol.RaizId; Vista.SeleccionId = id; }
        Vista.MostrarPersona(id, false);
        if (Vista.TarjetaDe(id) is { } tarjeta) MostrarMenu(id, tarjeta);
    }

    /// <summary>Abre (o cierra) el panel con las tres opciones de las tarjetas.</summary>
    private void Tarjetas_Click(object sender, RoutedEventArgs e) => TarjetasPopup.IsOpen = !TarjetasPopup.IsOpen;

    /// <summary>Se ha elegido una opción en el panel de las tarjetas.</summary>
    private void Tarjeta_Checked(object sender, RoutedEventArgs e)
    {
        if (Vista == null || _poniendoTarjetas) return;          // durante InitializeComponent, o al marcarlas desde PonerTarjetas
        PonerTarjetas(new FormaTarjeta(TarjetaVertical.IsChecked == true, TarjetaSinFoto.IsChecked != true, LetraGrande.IsChecked == true));
    }
    private bool _poniendoTarjetas;

    /// <summary>
    /// Cambia la forma de las tarjetas (horizontal o vertical, con foto o sin ella, letra pequeña o grande): marca sus
    /// opciones en el panel, dibuja el icono de la cabecera y se recuerda para la próxima vez.
    /// </summary>
    private void PonerTarjetas(FormaTarjeta f)
    {
        _poniendoTarjetas = true;
        (f.Vertical ? TarjetaVertical : TarjetaHorizontal).IsChecked = true;
        (f.Foto ? TarjetaConFoto : TarjetaSinFoto).IsChecked = true;
        (f.LetraGrande ? LetraGrande : LetraPequena).IsChecked = true;
        _poniendoTarjetas = false;
        DibujarIconoTarjetas(f);
        if (Vista.Tarjetas == f) return;
        Vista.Tarjetas = f;
        _prefs.Tarjetas = f.ToString(); _prefs.TarjetasEstrechas = false; _prefs.Guardar();
    }

    /// <summary>El icono del botón de las tarjetas: la tarjeta elegida, en miniatura.</summary>
    private void DibujarIconoTarjetas(FormaTarjeta f)
    {
        var c = TarjetasIcono;
        c.Children.Clear();
        var color = new System.Windows.Data.Binding("Foreground") { Source = TarjetasBtn };
        void Poner(System.Windows.Shapes.Shape forma, double x, double y, double grosor)
        {
            if (grosor > 0)
            {
                forma.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, color);
                forma.StrokeThickness = grosor;
                forma.StrokeStartLineCap = forma.StrokeEndLineCap = System.Windows.Media.PenLineCap.Round;
            }
            else forma.SetBinding(System.Windows.Shapes.Shape.FillProperty, color);
            System.Windows.Controls.Canvas.SetLeft(forma, x); System.Windows.Controls.Canvas.SetTop(forma, y);
            c.Children.Add(forma);
        }
        void Linea(double xa, double xb, double y, double grosor) =>
            Poner(new System.Windows.Shapes.Line { X1 = xa, Y1 = y, X2 = xb, Y2 = y }, 0, 0, grosor);

        double ancho = f.Vertical ? 13 : 19, alto = f.Vertical ? 18 : 13;
        double x0 = (20 - ancho) / 2, y0 = (20 - alto) / 2;
        Poner(new System.Windows.Shapes.Rectangle { Width = ancho, Height = alto, RadiusX = 2.5, RadiusY = 2.5 }, x0, y0, 1.4);
        // las líneas del texto: más gruesas con la letra grande
        double g1 = f.LetraGrande ? 2.3 : 1.4, g2 = f.LetraGrande ? 1.7 : 1.1;
        if (!f.Vertical)
        {
            double xt;
            if (f.Foto) { Poner(new System.Windows.Shapes.Ellipse { Width = 5.4, Height = 5.4 }, x0 + 2.6, y0 + 3.8, 1.2); xt = x0 + 10.2; }
            else { Poner(new System.Windows.Shapes.Rectangle { Width = 1.8, Height = alto - 5.6 }, x0 + 2.4, y0 + 2.8, 0); xt = x0 + 6.3; }
            Linea(xt, x0 + ancho - 3, y0 + 4.9, g1);
            Linea(xt, x0 + ancho - (f.LetraGrande ? 6.5 : 5.5), y0 + 8.3, g2);
        }
        else
        {
            double cx = x0 + ancho / 2, yt;
            if (f.Foto) { Poner(new System.Windows.Shapes.Ellipse { Width = 5.6, Height = 5.6 }, cx - 2.8, y0 + 2.6, 1.2); yt = y0 + 11.5; }
            else { Poner(new System.Windows.Shapes.Rectangle { Width = ancho - 6, Height = 1.8 }, x0 + 3, y0 + 2.4, 0); yt = y0 + 7; }
            Linea(x0 + 3, x0 + ancho - 3, yt, g1);
            Linea(cx - 2.4, cx + 2.4, yt + (f.LetraGrande ? 3.4 : 2.9), g2);
            if (!f.Foto) Linea(x0 + 3.8, x0 + ancho - 3.8, yt + (f.LetraGrande ? 6.8 : 5.8), g2);
        }
        TarjetasBtn.ToolTip = "Tarjetas " + Describir(f) + ". Pulsa para cambiarlas";
    }

    /// <summary>"horizontales, con foto y letra pequeña", etc.</summary>
    private static string Describir(FormaTarjeta f) =>
        $"{(f.Vertical ? "verticales" : "horizontales")}, {(f.Foto ? "con foto" : "sin foto")} y letra {(f.LetraGrande ? "grande" : "pequeña")}";

    /// <summary>Muestra u oculta la guía de impresión (la mejor forma de imprimir el árbol en el papel y las hojas elegidas).</summary>
    private void Guia_Click(object sender, RoutedEventArgs e)
    {
        Vista.GuiaVisible = !Vista.GuiaVisible;
        _prefs.Guia = Vista.GuiaVisible; _prefs.Guardar();
        MostrarGuia();
        if (Vista.GuiaVisible) Vista.Ajustar();      // para ver las hojas enteras
    }

    /// <summary>
    /// Imprime el árbol en el papel y el número de hojas de la guía, con la mejor colocación de las hojas y sus marcas de
    /// recortar y pegar. Si la guía no se estaba viendo, se muestra antes, para que se vea qué se va a imprimir.
    /// </summary>
    private void Imprimir_Click(object sender, RoutedEventArgs e)
    {
        if (!Vista.GuiaVisible) { Guia_Click(this, e); }
        if (Vista.GuiaActual is not { } c) return;
        try { Impresion.Imprimir(this, Arbol, Vista.Ordenacion, Vista.Tarjetas, !_prefs.ImpresionOscura, c); }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo imprimir", ex.Message); }
    }

    /// <summary>Estilo de la impresión: claro (tarjetas blancas, poca tinta) u oscuro (como en pantalla).</summary>
    private void Estilo_Opcion(object sender, RoutedEventArgs e)
    {
        if (_cargandoGuia) return;
        _prefs.ImpresionOscura = ImpresionOscura.IsChecked == true; _prefs.Guardar();
    }

    /// <summary>Cambio de papel o de número de hojas en el panel de la guía.</summary>
    private void Guia_Opcion(object sender, RoutedEventArgs e)
    {
        if (Vista == null || _cargandoGuia) return;
        var papel = new[] { PapelA4, PapelA3, PapelA2, PapelA1 }.First(r => r.IsChecked == true).Content.ToString()!;
        int hojas = Array.FindIndex(new[] { Hojas1, Hojas2, Hojas3, Hojas4 }, r => r.IsChecked == true) + 1;
        bool evitar = CortesIguales.IsChecked != true;
        if (papel == Vista.PapelGuia && hojas == Vista.HojasGuia && evitar == Vista.EvitarCortesGuia) return;
        Vista.PapelGuia = papel; Vista.HojasGuia = hojas; Vista.EvitarCortesGuia = evitar;
        _prefs.PapelGuia = papel; _prefs.HojasGuia = hojas; _prefs.CortesIguales = !evitar; _prefs.Guardar();
        Vista.Ajustar();
    }
    private bool _cargandoGuia;

    /// <summary>Pone la guía como estaba la última vez (papel, hojas y si se veía).</summary>
    private void CargarGuia()
    {
        _cargandoGuia = true;
        string papel = _prefs.PapelGuia is "A3" or "A2" or "A1" ? _prefs.PapelGuia : "A4";
        int hojas = Math.Clamp(_prefs.HojasGuia, 1, 4);
        Vista.PapelGuia = papel; Vista.HojasGuia = hojas; Vista.EvitarCortesGuia = !_prefs.CortesIguales;
        (_prefs.CortesIguales ? CortesIguales : CortesEntreTarjetas).IsChecked = true;
        (papel switch { "A3" => PapelA3, "A2" => PapelA2, "A1" => PapelA1, _ => PapelA4 }).IsChecked = true;
        new[] { Hojas1, Hojas2, Hojas3, Hojas4 }[hojas - 1].IsChecked = true;
        (_prefs.ImpresionOscura ? ImpresionOscura : ImpresionClara).IsChecked = true;
        Vista.GuiaVisible = _prefs.Guia;
        _cargandoGuia = false;
        MostrarGuia();
    }

    private void MostrarGuia()
    {
        GuiaBtn.Foreground = (System.Windows.Media.Brush)FindResource(Vista.GuiaVisible ? "AcentoBrush" : "TextoBrush");
        GuiaBtn.ToolTip = Vista.GuiaVisible
            ? "Modo de impresión activado: pulsa para salir (Ctrl+H)"
            : "Imprimir: ver cómo quedaría el árbol en hojas A4 a A1 y prepararlas (Ctrl+H)";
        GuiaPanel.Visibility = Vista.GuiaVisible ? Visibility.Visible : Visibility.Collapsed;
        GuiaPanel.UpdateLayout();
        Vista.MargenSuperior = Vista.GuiaVisible ? GuiaPanel.ActualHeight + GuiaPanel.Margin.Top : 0;
        GuiaTxt.Text = Vista.GuiaActual is { } g && Vista.Layout != null ? g.Descripcion(Vista.LetraNombre) : "";
    }

    /// <summary>Cambia el algoritmo de colocación; las tarjetas se deslizan y la persona seleccionada se queda a la vista.</summary>
    private void Ordenacion_Checked(object sender, RoutedEventArgs e)
    {
        if (Vista == null) return;          // durante InitializeComponent
        var o = OrdenacionE.IsChecked == true ? Ordenacion.E : OrdenacionD.IsChecked == true ? Ordenacion.D : OrdenacionC.IsChecked == true ? Ordenacion.C
              : OrdenacionB.IsChecked == true ? Ordenacion.B : Ordenacion.A;
        if (Vista.Ordenacion == o) return;
        Vista.Ordenacion = o;
        _prefs.Ordenacion = o.ToString(); _prefs.Guardar();
    }

    private void MostrarAtajos()
    {
        DialogoMensaje.Avisar(this, "Atajos de teclado",
            "Flechas  →  moverse por el árbol (↑ padres, ↓ hijos)\n" +
            "Intro / F2  →  editar la persona\n" +
            "Insert o +  →  añadir familiar (luego ↑↓ o 1-9)\n" +
            "Supr  →  eliminar (pide confirmación)\n" +
            "Ctrl+Intro  →  abrir su árbol enlazado\n" +
            "Ctrl+F  →  buscar personas (Intro / Mayús+Intro: siguiente / anterior)\n" +
            "Ctrl+V  →  pegar la imagen del portapapeles como foto\n" +
            "Arrastrar una imagen sobre una tarjeta  →  ponerle esa foto\n" +
            "Esc  →  quitar la selección\n\n" +
            "Ctrl+flechas  →  desplazar la vista\n" +
            "Ctrl + / Ctrl -  →  zoom        Ctrl+0  →  ver todo\n" +
            "Inicio  →  ir a la persona principal\n" +
            "Alt+←  →  volver al árbol anterior\n\n" +
            "Ctrl+L  →  pasar a la siguiente ordenación (Compacto, Lateral, Balanceado, Escalonado, Bowtie)\n" +
            "Ctrl+T  →  tarjetas horizontales o verticales (la foto encima del nombre)\n" +
            "Ctrl+Mayús+F  →  tarjetas con foto o sin ella (más pequeñas)\n" +
            "Ctrl+G  →  letra pequeña o grande (la que mejor se lee en papel)\n" +
            "Ctrl+H  →  modo de impresión: la mejor forma de imprimir el árbol en 1 a 4 hojas A4, A3, A2 o A1\n" +
            "Ctrl+P  →  imprimir las hojas de la guía, listas para recortar y pegar\n" +
            "Ctrl+Z / Ctrl+Y  →  deshacer / rehacer\n" +
            "Ctrl+S / Ctrl+O / Ctrl+N  →  guardar / abrir / nuevo");
    }

    // ---------- Edición ----------
    private bool EditarPersona(string id, bool esNueva)
    {
        var p = Arbol.Buscar(id);
        if (p == null) return false;
        var trabajo = p.Clonar();
        var dlg = new EditorPersona(trabajo, Doc.Ruta, esNueva, id == Arbol.RaizId) { Owner = this };
        bool ok = dlg.ShowDialog() == true;
        if (dlg.EliminarSolicitado) { EliminarPersona(id); return true; }
        if (!ok) return false;
        bool hayCambios = esNueva || trabajo.Nombre != p.Nombre || trabajo.Apellidos != p.Apellidos || trabajo.Sexo != p.Sexo
            || trabajo.Foto != p.Foto || trabajo.Historia != p.Historia || trabajo.ArbolEnlazado != p.ArbolEnlazado;
        if (hayCambios)
        {
            if (!esNueva) Doc.Registrar();
            p.Nombre = trabajo.Nombre; p.Apellidos = trabajo.Apellidos; p.Sexo = trabajo.Sexo;
            p.Foto = trabajo.Foto; p.Historia = trabajo.Historia; p.ArbolEnlazado = trabajo.ArbolEnlazado;
            Vista.Refrescar(true);
            ActualizarCabecera();
        }
        if (dlg.CrearArbolSolicitado) CrearArbolPropio(id);
        return true;
    }

    /// <summary>Aplica una alta y abre el editor sobre la persona nueva; si se cancela, se deshace todo.</summary>
    private void Anadir(Func<Arbol, Persona> op)
    {
        bool estabaModificado = Doc.Modificado;
        Doc.Registrar();
        Persona nueva;
        try { nueva = op(Arbol); }
        catch (Exception ex)
        {
            Doc.DescartarUltimo(); Doc.Modificado = estabaModificado;
            Vista.Cargar(Arbol, false);
            DialogoMensaje.Avisar(this, "No se puede añadir", ex.Message);
            return;
        }
        Vista.Refrescar(true);
        Vista.SeleccionId = nueva.Id;
        AsegurarVisible(nueva.Id);
        ActualizarCabecera();
        if (!EditarPersona(nueva.Id, true))
        {
            // Cancelado: se deshace el alta (salvo que el usuario haya optado por eliminar, que ya la gestionó).
            if (Arbol.Buscar(nueva.Id) != null)
            {
                Doc.DescartarUltimo(); Doc.Modificado = estabaModificado;
                Vista.Cargar(Arbol, false);
                Vista.SeleccionId = null;
                ActualizarCabecera();
            }
        }
    }

    private void AsegurarVisible(string id)
    {
        var r = Vista.RectDe(id);
        if (r == null) return;
        var v = Vista.Visible; v.Inflate(-60 / Vista.Escala, -60 / Vista.Escala);
        if (!v.Contains(r.Value)) Vista.CentrarEn(id, Math.Max(Vista.Escala, 0.55));
    }

    private void EliminarPersona(string id)
    {
        var p = Arbol.Buscar(id);
        if (p == null) return;
        if (id == Arbol.RaizId)
        {
            DialogoMensaje.Avisar(this, "No se puede eliminar", "Es la persona principal del árbol: este árbol es el suyo, así que no se la puede eliminar. Si quieres un árbol centrado en otra persona, créalo desde su tarjeta con «Crear su árbol con su familia…».");
            return;
        }
        var quitadas = Arbol.PersonasQueSeEliminarian(id);
        string nombre = string.IsNullOrWhiteSpace(p.NombreCompleto) ? "esta persona" : p.NombreCompleto;
        string msg = quitadas.Count == 1
            ? $"Se eliminará a {nombre}."
            : $"Se eliminará a {nombre} y también a {quitadas.Count - 1} persona{(quitadas.Count == 2 ? "" : "s")} que quedarían desconectadas del árbol:\n\n" +
              string.Join(", ", quitadas.Where(q => q.Id != id).Take(8).Select(q => string.IsNullOrWhiteSpace(q.NombreCompleto) ? "(sin nombre)" : q.NombreCompleto)) +
              (quitadas.Count > 9 ? "…" : "");
        if (DialogoMensaje.Preguntar(this, "Eliminar", msg + "\n\nPuedes deshacerlo con Ctrl+Z.", new[] { "Eliminar", "Cancelar" }, 0, 1, true) != 0) return;
        var borradas = quitadas.Select(q => q.Id).ToHashSet();
        var candidatos = new List<string>();
        if (Arbol.UnionComoHijo(id) is { } origen) { candidatos.AddRange(origen.Parejas); candidatos.AddRange(origen.Hijos); }
        candidatos.AddRange(Arbol.UnionesComoPareja(id).SelectMany(u => u.Parejas.Concat(u.Hijos)));
        candidatos.Add(Arbol.RaizId);
        var siguiente = candidatos.FirstOrDefault(c => c != id && !borradas.Contains(c));
        Doc.Registrar();
        Arbol.Eliminar(id);
        Vista.Refrescar(true);
        Vista.SeleccionId = siguiente;
        ActualizarCabecera();
    }

    // ---------- Menú «+» ----------
    private void MostrarMenu(string id, FrameworkElement ancla)
    {
        var a = Arbol; var p = a.Obtener(id);
        bool sang = a.EsSanguinea(id);
        const string ayudaPolitica = "La familia de una pareja política va en su propio árbol: usa «Abrir / crear su árbol».";
        var items = new List<ItemMenu>();

        var up = a.UnionComoHijo(id);
        if (up == null || up.Parejas.Count < 2)
        {
            string txt = up == null ? "Añadir padres" : a.Obtener(up.Parejas[0]).Sexo == Sexo.Hombre ? "Añadir madre" : "Añadir padre";
            items.Add(new ItemMenu("", txt, () => Anadir(x => x.AnadirPadres(id)[0]), sang, sang ? null : ayudaPolitica));
        }
        items.Add(new ItemMenu("", "Añadir hermano/a", () => Anadir(x => x.AnadirHermano(id).hermano), sang, sang ? null : ayudaPolitica));
        items.Add(new ItemMenu("", "Añadir pareja", () => Anadir(x => x.AnadirPareja(id))));

        var unions = a.UnionesComoPareja(id).ToList();
        if (unions.Count <= 1)
            items.Add(new ItemMenu("", "Añadir hijo/a", () => Anadir(x => x.AnadirHijo(id, null))));
        else
        {
            foreach (var u in unions)
            {
                var otro = u.Parejas.Where(q => q != id).Select(q => a.Obtener(q)).FirstOrDefault();
                string nom = otro == null ? "pareja desconocida" : string.IsNullOrWhiteSpace(otro.NombreCompleto) ? "pareja sin nombre" : otro.NombreCompleto;
                var uid = u.Id;
                items.Add(new ItemMenu("", $"Añadir hijo/a con {nom}", () => Anadir(x => x.AnadirHijo(id, uid))));
            }
            items.Add(new ItemMenu("", "Añadir hijo/a con otra pareja", () => Anadir(x => { var u = x.NuevaUnion(id); return x.AnadirHijo(id, u.Id); })));
        }

        items.Add(ItemMenu.Sep());
        items.Add(new ItemMenu("", "Editar…", () => EditarPersona(id, false)));
        if (id != a.RaizId)
        {
            if (!string.IsNullOrEmpty(p.ArbolEnlazado))
                items.Add(new ItemMenu("", "Abrir su árbol", () => AbrirEnlace(id)));
            else
                items.Add(new ItemMenu("", "Crear su árbol con su familia…", () => CrearArbolPropio(id)));
        }
        if (id != a.RaizId)
        {
            items.Add(ItemMenu.Sep());
            items.Add(new ItemMenu("", "Eliminar…", () => EliminarPersona(id), Peligro: true));
        }
        MenuFlotante.Mostrar(ancla, items);
    }

    // ---------- Árboles enlazados ----------
    private bool GuardarAntesDeEnlazar()
    {
        if (Doc.Ruta != null) return true;
        int r = DialogoMensaje.Preguntar(this, "Guarda este árbol primero",
            "Para enlazar o abrir otro árbol hay que guardar antes este, así las rutas quedan bien.", new[] { "Guardar…", "Cancelar" }, 0, 1);
        return r == 0 && GuardarDoc(Doc, false);
    }

    private void AbrirEnlace(string id)
    {
        var p = Arbol.Buscar(id);
        if (p == null || string.IsNullOrEmpty(p.ArbolEnlazado)) return;
        if (!GuardarAntesDeEnlazar()) return;
        var ruta = ArbolJson.ResolverRuta(Doc.Ruta!, p.ArbolEnlazado);
        if (!File.Exists(ruta))
        {
            int r = DialogoMensaje.Preguntar(this, "No se encuentra el árbol", $"No existe el archivo:\n{ruta}\n\n¿Quieres buscarlo?", new[] { "Buscar…", "Cancelar" }, 0, 1);
            if (r != 0) return;
            var dlg = new OpenFileDialog { Title = "Árbol de " + p.NombreCompleto, Filter = "Árbol genealógico (*.json)|*.json" };
            if (dlg.ShowDialog(this) != true) return;
            Doc.Registrar();
            p.ArbolEnlazado = ArbolJson.RutaRelativa(Doc.Ruta!, dlg.FileName);
            ruta = Path.GetFullPath(dlg.FileName);
            ActualizarCabecera();
        }
        int ya = _pila.FindIndex(d => d.Ruta != null && string.Equals(d.Ruta, ruta, StringComparison.OrdinalIgnoreCase));
        if (ya >= 0)
        {
            // Ya está abierto en la pila: se vuelve a él y se cierran los de encima; los que tengan cambios preguntan.
            foreach (var d in _pila.Skip(ya + 1).Reverse().ToList())
                if (!PreguntarGuardar(d, "antes de cerrarlo")) return;
            RecordarVista();
            while (_pila.Count - 1 > ya) _pila.RemoveAt(_pila.Count - 1);
            MostrarDocumento(true);
            return;
        }
        // Pasar a otro árbol: si este tiene cambios (p. ej. el enlace que se acaba de localizar), se pregunta si guardarlos.
        if (!PreguntarGuardar(Doc, "antes de ir a otro árbol")) return;
        CargarArchivo(ruta);
    }

    /// <summary>
    /// Crea el árbol propio de una persona llevándose a su familia directa y a sus cónyuges, enlaza ambos árboles,
    /// cierra el actual (tras asegurarse de que está guardado) y abre el nuevo.
    /// </summary>
    private void CrearArbolPropio(string id)
    {
        var p = Arbol.Buscar(id);
        if (p == null || id == Arbol.RaizId) return;     // el árbol de la persona principal es este mismo
        string nombre = string.IsNullOrWhiteSpace(p.NombreCompleto) ? "esta persona" : p.NombreCompleto;

        var nuevo = Arbol.ExtraerFamiliaDe(id);
        int total = nuevo.Personas.Count;
        string quien = total == 1
            ? "Esta persona no tiene familiares en este árbol: el árbol nuevo empezará solo con ella."
            : $"Se llevará a {nombre} y a {total - 1} persona{(total == 2 ? "" : "s")} más: sus antepasados y todos sus familiares de sangre (hermanos, tíos, primos, sobrinos…), más los cónyuges de todos ellos.";
        // El dueño de este árbol (su persona principal), si viaja al nuevo, enlazará allí con este árbol.
        var dueno = Arbol.Buscar(Arbol.RaizId);
        bool duenoViaja = dueno != null && nuevo.Buscar(dueno.Id) != null;
        string duenoTxt = duenoViaja
            ? $" En el árbol nuevo, la tarjeta de {(string.IsNullOrWhiteSpace(dueno!.NombreCompleto) ? "la persona principal" : dueno.NombreCompleto)} quedará enlazada con este árbol."
            : "";
        int r = DialogoMensaje.Preguntar(this, "Crear su árbol",
            quien + "\n\nEs una copia: este árbol conserva a todos. La tarjeta de " + nombre + " quedará enlazada con el árbol nuevo, " +
            "que se abrirá en lugar de este." + duenoTxt,
            new[] { "Continuar…", "Cancelar" }, 0, 1);
        if (r != 0) return;

        // 1. Este árbol debe estar guardado (el enlace nuevo se guardará en él).
        if (Doc.Modificado || Doc.Ruta == null)
        {
            int g = DialogoMensaje.Preguntar(this, "Guarda este árbol primero",
                $"«{Doc.Titulo}» tiene cambios sin guardar. Hay que guardarlos antes de crear el árbol nuevo.",
                new[] { "Guardar y continuar", "Cancelar" }, 0, 1);
            if (g != 0 || !GuardarDoc(Doc, false)) return;
        }

        // 2. Dónde guardar el árbol nuevo.
        var dlg = new SaveFileDialog
        {
            Title = "Crear el árbol de " + nombre, Filter = "Árbol genealógico (*.json)|*.json", DefaultExt = ".json", AddExtension = true,
            FileName = "Familia de " + Limpiar(nombre) + ".json",
            InitialDirectory = Path.GetDirectoryName(Doc.Ruta),
        };
        if (dlg.ShowDialog(this) != true) return;
        var ruta = Path.GetFullPath(dlg.FileName);
        var rutaActual = Doc.Ruta!;
        if (string.Equals(ruta, rutaActual, StringComparison.OrdinalIgnoreCase))
        {
            DialogoMensaje.Avisar(this, "Elige otro archivo", "El árbol nuevo no puede sobrescribir el árbol actual.");
            return;
        }

        // 3. Árbol nuevo: nombre, enlaces relativos bien rebasados y enlace de vuelta. "Árbol propio" de una persona es SU
        //    árbol: el que enlaza con este árbol es el dueño de este (su persona principal), no la persona del árbol nuevo.
        nuevo.Nombre = "Familia de " + nombre;
        RebasarEnlaces(nuevo, rutaActual, ruta);
        if (nuevo.Buscar(Arbol.RaizId) is { } duenoEnNuevo && duenoEnNuevo.Id != nuevo.RaizId && string.IsNullOrEmpty(duenoEnNuevo.ArbolEnlazado))
            duenoEnNuevo.ArbolEnlazado = ArbolJson.RutaRelativa(ruta, rutaActual);
        try { ArbolJson.Guardar(ruta, nuevo); }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo crear el árbol", ex.Message); return; }

        // 4. Enlace en este árbol (se guarda ya) y cambio de árbol: se abre el nuevo y se cierra este.
        Doc.Registrar();
        p.ArbolEnlazado = ArbolJson.RutaRelativa(rutaActual, ruta);
        if (!GuardarDoc(Doc, false)) return;
        var anterior = Doc;
        if (!CargarArchivo(ruta)) return;
        _pila.Remove(anterior);
        if (_pila.Count == 1) { _prefs.UltimoArchivo = Doc.Ruta; _prefs.Guardar(); }
        Vista.SeleccionId = Arbol.RaizId;
        ActualizarCabecera();
    }
}
