using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using Microsoft.Win32;

namespace ArbolGenealogico.App;

public partial class EditorPersona : Window
{
    private readonly Persona _persona;
    private readonly string? _rutaJson;
    private string? _foto;
    private string? _enlace;
    private readonly bool _puedeCrearArbol;

    /// <summary>El usuario ha pedido eliminar a la persona.</summary>
    public bool EliminarSolicitado { get; private set; }

    /// <summary>El usuario ha pedido crear el árbol de esta persona (los cambios del diálogo se aplican antes).</summary>
    public bool CrearArbolSolicitado { get; private set; }

    /// <param name="esPrincipal">Es la persona principal del árbol: su árbol es este mismo, así que no hay opciones de archivo ni se puede eliminar.</param>
    public EditorPersona(Persona persona, string? rutaJson, bool esNueva, bool esPrincipal)
    {
        InitializeComponent();
        Dwm.Aplicar(this);
        _persona = persona; _rutaJson = rutaJson;
        Title = esNueva ? "Nueva persona" : "Editar persona";
        NombreBox.Text = persona.Nombre;
        ApellidosBox.Text = persona.Apellidos;
        HistoriaBox.Text = persona.Historia;
        SexoH.IsChecked = persona.Sexo == Sexo.Hombre;
        SexoM.IsChecked = persona.Sexo == Sexo.Mujer;
        SexoU.IsChecked = persona.Sexo == Sexo.Desconocido;
        // Solo el botón marcado es parada del tabulador: Tab entra y sale del grupo, y las flechas cambian la opción.
        foreach (var o in new[] { SexoH, SexoM, SexoU })
        {
            o.IsTabStop = o.IsChecked == true;
            o.Checked += (_, _) => { foreach (var r in new[] { SexoH, SexoM, SexoU }) r.IsTabStop = r.IsChecked == true; };
        }
        _foto = persona.Foto;
        _enlace = esPrincipal ? null : persona.ArbolEnlazado;
        _puedeCrearArbol = !esPrincipal && !esNueva;
        SeccionArbol.Visibility = esPrincipal ? Visibility.Collapsed : Visibility.Visible;
        EliminarBtn.Visibility = !esPrincipal && !esNueva ? Visibility.Visible : Visibility.Collapsed;
        MostrarFoto(); MostrarEnlace();
        Loaded += (_, _) => { NombreBox.Focus(); NombreBox.SelectAll(); };
        NombreBox.TextChanged += (_, _) => MostrarFoto();
        ApellidosBox.TextChanged += (_, _) => MostrarFoto();
    }

    private void MostrarFoto()
    {
        var img = Fotos.Cargar(_foto);
        if (img != null)
        {
            FotoCirculo.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            FotoIniciales.Text = "";
        }
        else
        {
            FotoCirculo.Fill = new SolidColorBrush(Color.FromRgb(0x2B, 0x30, 0x40));
            FotoIniciales.Text = Fotos.Iniciales(new Persona { Nombre = NombreBox.Text, Apellidos = ApellidosBox.Text });
        }
        QuitarFotoBtn.IsEnabled = _foto != null;
    }

    private void PonerFoto(Func<string?> origen)
    {
        try
        {
            var foto = origen();
            if (foto == null) return;
            _foto = foto; MostrarFoto();
        }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo cargar la foto", ex.Message); }
    }

    private void Ventana_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = Fotos.DatosTienenImagen(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Ventana_Drop(object sender, DragEventArgs e)
    {
        if (Fotos.DatosTienenImagen(e.Data)) PonerFoto(() => Fotos.DesdeDatos(e.Data));
        e.Handled = true;
    }

    /// <summary>Ctrl+V pega una imagen como foto, salvo que se esté escribiendo y haya texto en el portapapeles.</summary>
    private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        bool escribiendo = Keyboard.FocusedElement is System.Windows.Controls.TextBox && Clipboard.ContainsText();
        if (escribiendo || !Fotos.PortapapelesTieneImagen()) return;
        PonerFoto(Fotos.DesdePortapapeles);
        e.Handled = true;
    }

    /// <summary>Con el foco en el sexo, las flechas cambian entre Hombre / Mujer / Sin especificar.</summary>
    private void Sexo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        int paso = e.Key is Key.Right or Key.Down ? 1 : e.Key is Key.Left or Key.Up ? -1 : 0;
        if (paso == 0) return;
        var opciones = new[] { SexoH, SexoM, SexoU };
        int actual = Array.FindIndex(opciones, o => o.IsKeyboardFocused);
        if (actual < 0) actual = Array.FindIndex(opciones, o => o.IsChecked == true);
        var destino = opciones[Math.Clamp(actual + paso, 0, opciones.Length - 1)];
        destino.IsChecked = true;
        destino.Focus();
        e.Handled = true;
    }

    private void MostrarEnlace()
    {
        EnlaceBox.Text = _enlace ?? "";
        // Crear su árbol solo tiene sentido si aún no tiene uno enlazado.
        CrearArbolBtn.Visibility = _puedeCrearArbol && string.IsNullOrWhiteSpace(_enlace) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CrearArbol_Click(object sender, RoutedEventArgs e)
    {
        CrearArbolSolicitado = true;
        Guardar_Click(sender, e);
    }

    private void ElegirFoto_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Elegir foto", Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp|Todos los archivos|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        try { _foto = Fotos.Importar(dlg.FileName); MostrarFoto(); }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo cargar la foto", ex.Message); }
    }

    private void QuitarFoto_Click(object sender, RoutedEventArgs e) { _foto = null; MostrarFoto(); }

    private void ElegirEnlace_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Árbol de esta persona", Filter = "Árbol genealógico (*.json)|*.json|Todos|*.*" };
        if (_rutaJson != null) dlg.InitialDirectory = Path.GetDirectoryName(_rutaJson);
        if (dlg.ShowDialog(this) != true) return;
        _enlace = _rutaJson != null ? ArbolJson.RutaRelativa(_rutaJson, dlg.FileName) : dlg.FileName;
        MostrarEnlace();
    }

    private void QuitarEnlace_Click(object sender, RoutedEventArgs e) { _enlace = null; MostrarEnlace(); }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        _persona.Nombre = NombreBox.Text.Trim();
        _persona.Apellidos = ApellidosBox.Text.Trim();
        _persona.Historia = HistoriaBox.Text.Trim();
        _persona.Sexo = SexoH.IsChecked == true ? Sexo.Hombre : SexoM.IsChecked == true ? Sexo.Mujer : Sexo.Desconocido;
        _persona.Foto = _foto;
        _persona.ArbolEnlazado = string.IsNullOrWhiteSpace(_enlace) ? null : _enlace;
        DialogResult = true;
    }

    private void Eliminar_Click(object sender, RoutedEventArgs e)
    {
        EliminarSolicitado = true;
        DialogResult = false;
    }
}
