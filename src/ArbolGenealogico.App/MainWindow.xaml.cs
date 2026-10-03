using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using Microsoft.Win32;

namespace ArbolGenealogico.App;

public partial class MainWindow : Window
{
    private readonly List<Documento> _pila = new();
    private readonly Preferencias _prefs = Preferencias.Cargar();
    private Documento Doc => _pila[^1];
    private Arbol Arbol => Doc.Arbol;

    public MainWindow(string? archivoInicial)
    {
        InitializeComponent();
        Dwm.Aplicar(this);
        Mini.Vista = Vista;

        Vista.SeleccionCambiada += _ => ActualizarCabecera();
        Vista.EditarSolicitado += id => EditarPersona(id, false);
        Vista.MenuSolicitado += MostrarMenu;
        Vista.AbrirEnlaceSolicitado += AbrirEnlace;
        Vista.CamaraCambiada += () => AyudaTxt.Opacity = Vista.Escala < 0.5 ? 0.0 : 1.0;

        PreviewKeyDown += AlTeclear;
        Drop += AlSoltarArchivo;
        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };

        var abierto = false;
        foreach (var ruta in new[] { archivoInicial, _prefs.UltimoArchivo })
        {
            if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) continue;
            if (CargarArchivo(ruta, silencioso: ruta == _prefs.UltimoArchivo, reemplazar: true)) { abierto = true; break; }
        }
        if (!abierto) AbrirDocumentoNuevo();
    }

    // ---------- Documentos ----------
    private void MostrarDocumento(bool ajustar)
    {
        Vista.SeleccionId = null;
        Vista.Cargar(Arbol, ajustar);
        ActualizarCabecera();
    }

    private void AbrirDocumentoNuevo()
    {
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

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmarCerrarTodo()) { e.Cancel = true; return; }
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
        var d = Doc;
        if (d.Modificado)
        {
            int r = DialogoMensaje.Preguntar(this, "Cambios sin guardar", $"«{d.Titulo}» tiene cambios sin guardar. ¿Quieres guardarlos antes de volver?",
                new[] { "Guardar", "No guardar", "Cancelar" }, 0, 2);
            if (r == 0) { if (!GuardarDoc(d, false)) return; }
            else if (r != 1) return;
        }
        _pila.RemoveAt(_pila.Count - 1);
        MostrarDocumento(false);
        if (Vista.SeleccionId != null) Vista.CentrarEn(Vista.SeleccionId, null, false);
        else Vista.Ajustar(false);
    }

    private void AlSoltarArchivo(object s, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        var ruta = files.FirstOrDefault(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (ruta != null && ConfirmarCerrarTodo()) CargarArchivo(ruta, reemplazar: true);
    }

    private void AlTeclear(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), mayus = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (ctrl && key == Key.S) { GuardarDoc(Doc, mayus); e.Handled = true; }
        else if (ctrl && key == Key.O) { Abrir_Click(this, e); e.Handled = true; }
        else if (ctrl && key == Key.N) { Nuevo_Click(this, e); e.Handled = true; }
        else if (ctrl && key == Key.Z) { if (mayus) Rehacer(); else Deshacer(); e.Handled = true; }
        else if (ctrl && key == Key.Y) { Rehacer(); e.Handled = true; }
        else if (ctrl && (key == Key.D0 || key == Key.NumPad0)) { Vista.Ajustar(); e.Handled = true; }
        else if (key == Key.Home) { CentrarPrincipal_Click(this, e); e.Handled = true; }
        else if (key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) { Atras(); e.Handled = true; }
        else if (key is Key.Add or Key.OemPlus) { Vista.Zoom(1.3); e.Handled = true; }
        else if (key is Key.Subtract or Key.OemMinus) { Vista.Zoom(1 / 1.3); e.Handled = true; }
        else if (Vista.SeleccionId is { } id)
        {
            if (key is Key.F2 or Key.Enter) { EditarPersona(id, false); e.Handled = true; }
            else if (key == Key.Delete) { EliminarPersona(id); e.Handled = true; }
        }
    }

    // ---------- Edición ----------
    private bool EditarPersona(string id, bool esNueva)
    {
        var p = Arbol.Buscar(id);
        if (p == null) return false;
        var trabajo = p.Clonar();
        var dlg = new EditorPersona(trabajo, Doc.Ruta, esNueva, id != Arbol.RaizId) { Owner = this };
        bool ok = dlg.ShowDialog() == true;
        if (dlg.EliminarSolicitado) { EliminarPersona(id); return true; }
        if (!ok) return false;
        if (!esNueva) Doc.Registrar();
        p.Nombre = trabajo.Nombre; p.Apellidos = trabajo.Apellidos; p.Sexo = trabajo.Sexo;
        p.Foto = trabajo.Foto; p.Historia = trabajo.Historia; p.ArbolEnlazado = trabajo.ArbolEnlazado;
        Vista.Refrescar(true);
        ActualizarCabecera();
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
            DialogoMensaje.Avisar(this, "No se puede eliminar", "Es la persona principal del árbol. Establece antes a otra persona como principal.");
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
        Doc.Registrar();
        Arbol.Eliminar(id);
        Vista.SeleccionId = null;
        Vista.Refrescar(true);
        ActualizarCabecera();
    }

    private void EstablecerPrincipal(string id)
    {
        if (!Arbol.PuedeSerPrincipal(id))
        {
            DialogoMensaje.Avisar(this, "No se puede cambiar la persona principal",
                "Esta persona no es de la línea sanguínea de quienes aparecen con padres en el árbol. Para verla como centro, abre o crea su propio árbol.");
            return;
        }
        Doc.Registrar();
        Arbol.RaizId = id;
        Vista.Refrescar(true);
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
        if (!string.IsNullOrEmpty(p.ArbolEnlazado))
            items.Add(new ItemMenu("", "Abrir su árbol", () => AbrirEnlace(id)));
        else
            items.Add(new ItemMenu("", sang ? "Crear un árbol propio de esta persona…" : "Crear su árbol (su familia)…", () => CrearArbolPropio(id)));
        if (id != a.RaizId)
            items.Add(new ItemMenu("", "Establecer como persona principal", () => EstablecerPrincipal(id)));
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
            // Ya está abierto en la pila: se vuelve a él.
            while (_pila.Count - 1 > ya) _pila.RemoveAt(_pila.Count - 1);
            MostrarDocumento(true);
            return;
        }
        CargarArchivo(ruta);
    }

    private void CrearArbolPropio(string id)
    {
        var p = Arbol.Buscar(id);
        if (p == null || !GuardarAntesDeEnlazar()) return;
        string nombre = string.IsNullOrWhiteSpace(p.NombreCompleto) ? "Persona" : p.NombreCompleto;
        var dlg = new SaveFileDialog
        {
            Title = "Crear el árbol de " + nombre, Filter = "Árbol genealógico (*.json)|*.json", DefaultExt = ".json", AddExtension = true,
            FileName = "Familia " + Limpiar(nombre) + ".json", InitialDirectory = Path.GetDirectoryName(Doc.Ruta),
        };
        if (dlg.ShowDialog(this) != true) return;
        var ruta = Path.GetFullPath(dlg.FileName);

        var nuevo = new Arbol { Nombre = "Familia " + (string.IsNullOrWhiteSpace(p.Apellidos) ? nombre : p.Apellidos) };
        var raiz = nuevo.NuevaPersona(p.Sexo);
        raiz.Nombre = p.Nombre; raiz.Apellidos = p.Apellidos; raiz.Foto = p.Foto; raiz.Historia = p.Historia;
        nuevo.RaizId = raiz.Id;
        try { ArbolJson.Guardar(ruta, nuevo); }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo crear el árbol", ex.Message); return; }

        Doc.Registrar();
        p.ArbolEnlazado = ArbolJson.RutaRelativa(Doc.Ruta!, ruta);
        Vista.Refrescar(false);
        ActualizarCabecera();
        CargarArchivo(ruta);
    }
}
