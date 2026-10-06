using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using Microsoft.Win32;

namespace ArbolGenealogico.App;

/// <summary>
/// Guardar el árbol como imagen: qué parte (todo o lo que se ve), en qué tema, a qué tamaño, en PNG o JPG, con fondo o sin
/// él y con el nombre del árbol encima o sin él. Una vista previa enseña cómo saldrá, y debajo, cuántos píxeles tendrá y lo
/// grandes que saldrán los nombres.
/// </summary>
public partial class ExportarImagen : Window
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    private readonly Arbol _arbol;
    private readonly Ordenacion _ordenacion;
    private readonly FormaTarjeta _tarjetas;
    private readonly Rect? _visible;
    private readonly string? _rutaArbol;
    private readonly Preferencias _prefs;
    /// <summary>El árbol dibujado en el tema elegido (se rehace al cambiar de tema).</summary>
    private ImagenArbol? _imagen;
    private bool _cargando = true, _previaPendiente, _guardando;

    /// <summary>Dónde se ha guardado la imagen (si se ha guardado).</summary>
    public string? RutaGuardada { get; private set; }

    /// <param name="visible">La parte del árbol que se ve en la ventana (coordenadas del árbol), o null si no se ve nada.</param>
    public ExportarImagen(Arbol arbol, Ordenacion ordenacion, FormaTarjeta tarjetas, Rect? visible, string? rutaArbol, Preferencias prefs)
    {
        InitializeComponent();
        Dwm.Aplicar(this);
        _arbol = arbol; _ordenacion = ordenacion; _tarjetas = tarjetas; _visible = visible; _rutaArbol = rutaArbol; _prefs = prefs;

        var tema = Enum.TryParse<EstiloPapel>(prefs.ImagenTema, out var t) ? t : EstiloPapel.Oscuro;
        (tema switch { EstiloPapel.Claro => TemaClaro, EstiloPapel.BlancoYNegro => TemaByN, _ => TemaOscuro }).IsChecked = true;
        (BotonesTamano.FirstOrDefault(b => Escala(b) == prefs.ImagenEscala) ?? Tamano2).IsChecked = true;
        (prefs.ImagenJpg ? FormatoJpg : FormatoPng).IsChecked = true;
        Transparente.IsChecked = prefs.ImagenTransparente;
        bool hayNombre = !string.IsNullOrWhiteSpace(arbol.Nombre);
        Titulo.Content = hayNombre ? $"Con el nombre del árbol encima: «{arbol.Nombre.Trim()}»" : "Con el nombre del árbol encima (este árbol aún no tiene nombre: guárdalo primero)";
        Titulo.IsEnabled = hayNombre;
        Titulo.IsChecked = hayNombre && prefs.ImagenTitulo;
        ZonaVista.IsEnabled = visible != null;
        _cargando = false;
        Loaded += (_, _) => Actualizar();
    }

    private RadioButton[] BotonesTamano => new[] { Tamano05, Tamano1, Tamano2, Tamano3 };
    private static double Escala(RadioButton b) => double.Parse((string)b.Tag, CultureInfo.InvariantCulture);

    private EstiloPapel TemaElegido => TemaClaro.IsChecked == true ? EstiloPapel.Claro : TemaByN.IsChecked == true ? EstiloPapel.BlancoYNegro : EstiloPapel.Oscuro;
    private bool Jpg => FormatoJpg.IsChecked == true;
    private bool SinFondo => !Jpg && Transparente.IsChecked == true;
    private bool ConTitulo => Titulo.IsChecked == true;
    private RadioButton? TamanoElegido => BotonesTamano.FirstOrDefault(b => b.IsChecked == true);

    private ImagenArbol Imagen
    {
        get
        {
            if (_imagen == null || _imagen.Tema != TemaElegido) _imagen = new ImagenArbol(_arbol, _ordenacion, _tarjetas, TemaElegido);
            return _imagen;
        }
    }

    private Rect Zona => ZonaVista.IsChecked == true && _visible is { } v ? v : Imagen.Entero;

    private void Opcion(object sender, RoutedEventArgs e)
    {
        if (_cargando || !IsLoaded) return;
        Actualizar();
    }

    /// <summary>Recalcula los tamaños posibles y el texto, y pide la vista previa.</summary>
    private void Actualizar()
    {
        Transparente.IsEnabled = !Jpg;
        var tam = Imagen.Tamano(Zona, ConTitulo);

        // Los tamaños que no caben (demasiados píxeles) no se pueden elegir; si el elegido no cabe, el mayor que quepa.
        foreach (var b in BotonesTamano)
        {
            var (w, h) = ImagenArbol.Pixeles(tam, Escala(b));
            b.IsEnabled = ImagenArbol.Cabe(w, h);
        }
        if (TamanoElegido is not { IsEnabled: true })
        {
            _cargando = true;
            var mayor = BotonesTamano.LastOrDefault(b => b.IsEnabled);
            if (mayor != null) mayor.IsChecked = true;
            _cargando = false;
        }
        GuardarBtn.IsEnabled = TamanoElegido is { IsEnabled: true } && !_guardando;

        if (TamanoElegido is { IsEnabled: true } t)
        {
            double escala = Escala(t);
            var (w, h) = ImagenArbol.Pixeles(tam, escala);
            double letra = Imagen.LetraNombre * escala;
            InfoTxt.Text = $"{w.ToString("N0", Es)} × {h.ToString("N0", Es)} píxeles  ·  los nombres, de unos {letra:0} píxeles de alto";
            var consejos = new List<string>();
            if (letra < 14) consejos.Add("A este tamaño los nombres salen pequeños: para leerlos habrá que ampliar la imagen.");
            if ((double)w * h > 40e6) consejos.Add("Es una imagen muy grande: puede tardar unos segundos en guardarse y algunos programas no la abren bien.");
            if (ZonaTodo.IsChecked == true && tam.Width > 4 * tam.Height && _ordenacion != Ordenacion.D)
                consejos.Add("El árbol es muy ancho: con la ordenación Escalonado sale más cuadrado, mejor para verlo en una pantalla o imprimirlo.");
            ConsejoTxt.Text = string.Join("\n", consejos);
            ConsejoTxt.Visibility = consejos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            InfoTxt.Text = "El árbol es demasiado grande para guardarlo como imagen así: prueba con «Lo que se ve ahora».";
            ConsejoTxt.Visibility = Visibility.Collapsed;
        }
        PedirPrevia();
    }

    /// <summary>La vista previa se rehace una sola vez aunque cambien varias cosas seguidas.</summary>
    private void PedirPrevia()
    {
        if (_previaPendiente) return;
        _previaPendiente = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => { _previaPendiente = false; DibujarPrevia(); });
    }

    private void DibujarPrevia()
    {
        var tam = Imagen.Tamano(Zona, ConTitulo);
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double ancho = PreviaBorde.ActualWidth - 22, alto = PreviaBorde.ActualHeight - 22;
        if (ancho <= 0 || alto <= 0 || tam.Width <= 0 || tam.Height <= 0) return;
        double s = Math.Min(1, Math.Min(ancho / tam.Width, alto / tam.Height));
        Previa.Source = Imagen.Dibujar(Zona, ConTitulo, s * dpi, SinFondo);
        Previa.Width = tam.Width * s; Previa.Height = tam.Height * s;
        PreviaAjedrez.Visibility = SinFondo ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Previa_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PreviaAjedrez.Width = Previa.ActualWidth; PreviaAjedrez.Height = Previa.ActualHeight;
    }

    private async void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (TamanoElegido is not { IsEnabled: true } t) return;
        string ext = Jpg ? ".jpg" : ".png";
        var dlg = new SaveFileDialog
        {
            Title = "Guardar como imagen",
            Filter = Jpg ? "Imagen JPG (*.jpg)|*.jpg" : "Imagen PNG (*.png)|*.png",
            DefaultExt = ext, AddExtension = true,
            FileName = NombreArchivo(_arbol.Nombre) + (ZonaVista.IsChecked == true ? " (parte)" : "") + ext,
        };
        var carpeta = _prefs.CarpetaImagenes ?? (_rutaArbol != null ? Path.GetDirectoryName(_rutaArbol) : null);
        if (carpeta != null && Directory.Exists(carpeta)) dlg.InitialDirectory = carpeta;
        if (dlg.ShowDialog(this) != true) return;
        var ruta = dlg.FileName;

        // Recordar las opciones para la próxima vez.
        _prefs.ImagenTema = TemaElegido.ToString(); _prefs.ImagenEscala = Escala(t); _prefs.ImagenJpg = Jpg;
        _prefs.ImagenTransparente = Transparente.IsChecked == true; _prefs.ImagenTitulo = ConTitulo;
        _prefs.CarpetaImagenes = Path.GetDirectoryName(ruta);
        _prefs.Guardar();

        _guardando = true;
        IsEnabled = false; Cursor = Cursors.Wait;
        EstadoTxt.Text = "Preparando la imagen…";
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);     // que se vea el aviso antes de ponerse a dibujar
            var bmp = Imagen.Dibujar(Zona, ConTitulo, Escala(t), SinFondo);
            EstadoTxt.Text = "Guardando…";
            bool sinFondo = SinFondo;
            await Task.Run(() => ImagenArbol.Guardar(bmp, ruta, sinFondo));
            RutaGuardada = ruta;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            _guardando = false;
            IsEnabled = true; Cursor = null; EstadoTxt.Text = "";
            DialogoMensaje.Avisar(this, "No se pudo guardar la imagen",
                ex is OutOfMemoryException ? "No hay memoria suficiente para una imagen tan grande. Prueba con un tamaño menor." : ex.Message);
        }
    }

    private static string NombreArchivo(string? s)
    {
        s ??= "";
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrWhiteSpace(s) ? "arbol" : s.Trim();
    }
}
