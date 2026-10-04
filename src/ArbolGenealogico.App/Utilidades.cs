using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.App;

/// <summary>Barra de título oscura en Windows 10/11.</summary>
public static class Dwm
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valor, int tam);

    public static void Aplicar(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                int uno = 1;
                DwmSetWindowAttribute(h, 20, ref uno, sizeof(int));            // modo oscuro
                int fondo = 0x16 << 16 | 0x10 << 8 | 0x0E;                      // #0E1016 como COLORREF (BGR)
                DwmSetWindowAttribute(h, 35, ref fondo, sizeof(int));           // color de la barra (Win11)
                int texto = 0xF4 << 16 | 0xEC << 8 | 0xE9;
                DwmSetWindowAttribute(h, 36, ref texto, sizeof(int));
            }
            catch { /* versiones antiguas: se queda la barra por defecto */ }
        };
    }
}

public static class Fotos
{
    private static readonly ConditionalWeakTable<string, ImageSource> Cache = new();
    public const int LadoMaximo = 320;

    public static ImageSource? Cargar(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        if (Cache.TryGetValue(base64, out var img)) return img;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            Cache.Add(base64, bmp);
            return bmp;
        }
        catch { return null; }
    }

    private static readonly string[] Extensiones = { ".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

    public static bool EsImagen(string? ruta) =>
        !string.IsNullOrEmpty(ruta) && Extensiones.Contains(Path.GetExtension(ruta).ToLowerInvariant());

    /// <summary>Lee una imagen del disco (respetando su orientación EXIF), la reduce a ≤320 px y la devuelve como JPEG en base64.</summary>
    public static string? Importar(string ruta)
    {
        var marco = BitmapFrame.Create(new Uri(Path.GetFullPath(ruta)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return Importar(Orientar(marco, LeerOrientacion(marco)));
    }

    /// <summary>Reduce una imagen en memoria a ≤320 px y la devuelve como JPEG en base64.</summary>
    public static string? Importar(BitmapSource origen)
    {
        BitmapSource fuente = origen;
        if (TieneAlfa(fuente)) fuente = SobreBlanco(fuente);
        double f = Math.Min(1.0, (double)LadoMaximo / Math.Max(fuente.PixelWidth, fuente.PixelHeight));
        if (f < 1) fuente = new TransformedBitmap(fuente, new ScaleTransform(f, f));
        if (fuente.Format != PixelFormats.Bgr24) fuente = new FormatConvertedBitmap(fuente, PixelFormats.Bgr24, null, 0);
        var enc = new JpegBitmapEncoder { QualityLevel = 85 };
        enc.Frames.Add(BitmapFrame.Create(fuente));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static bool TieneAlfa(BitmapSource s) =>
        s.Format == PixelFormats.Bgra32 || s.Format == PixelFormats.Pbgra32 || s.Format == PixelFormats.Prgba64
        || s.Format == PixelFormats.Rgba64 || s.Format == PixelFormats.Prgba128Float || s.Format == PixelFormats.Rgba128Float;

    private static BitmapSource SobreBlanco(BitmapSource s)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, s.PixelWidth, s.PixelHeight));
            dc.DrawImage(s, new Rect(0, 0, s.PixelWidth, s.PixelHeight));
        }
        var rtb = new RenderTargetBitmap(s.PixelWidth, s.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private static int LeerOrientacion(BitmapFrame marco)
    {
        try
        {
            if (marco.Metadata is BitmapMetadata md && md.ContainsQuery("System.Photo.Orientation"))
                return Convert.ToInt32(md.GetQuery("System.Photo.Orientation"));
        }
        catch { /* sin metadatos: se deja como está */ }
        return 1;
    }

    /// <summary>Aplica la orientación EXIF (las fotos de móvil suelen venir "de lado").</summary>
    private static BitmapSource Orientar(BitmapSource s, int orientacion)
    {
        Transform? t = orientacion switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
            6 => new RotateTransform(90),
            7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
            8 => new RotateTransform(270),
            _ => null,
        };
        return t == null ? s : new TransformedBitmap(s, t);
    }

    // ---------- Portapapeles y arrastrar-soltar ----------
    public static bool PortapapelesTieneImagen()
    {
        for (int i = 0; i < 4; i++)
        {
            try
            {
                return Clipboard.ContainsImage()
                    || (Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Cast<string>().Any(EsImagen));
            }
            catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(40); }
        }
        return false;
    }

    /// <summary>Foto (base64) de la imagen o del archivo de imagen que haya en el portapapeles; null si no hay.</summary>
    public static string? DesdePortapapeles()
    {
        for (int i = 0; i < 4; i++)
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var archivo = Clipboard.GetFileDropList().Cast<string>().FirstOrDefault(EsImagen);
                    if (archivo != null) return Importar(archivo);
                }
                if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } img) return Importar(img);
                return null;
            }
            catch (System.Runtime.InteropServices.COMException) { Thread.Sleep(40); }
        }
        return null;
    }

    public static bool DatosTienenImagen(IDataObject datos)
    {
        try
        {
            if (datos.GetDataPresent(DataFormats.FileDrop) && datos.GetData(DataFormats.FileDrop) is string[] fs && fs.Any(EsImagen)) return true;
            return datos.GetDataPresent(DataFormats.Bitmap);
        }
        catch { return false; }
    }

    public static string? DesdeDatos(IDataObject datos)
    {
        if (datos.GetDataPresent(DataFormats.FileDrop) && datos.GetData(DataFormats.FileDrop) is string[] fs)
        {
            var archivo = fs.FirstOrDefault(EsImagen);
            if (archivo != null) return Importar(archivo);
        }
        if (datos.GetDataPresent(DataFormats.Bitmap) && datos.GetData(DataFormats.Bitmap) is BitmapSource bs) return Importar(bs);
        return null;
    }

    public static string Iniciales(Persona p)
    {
        string Ini(string s) => string.IsNullOrWhiteSpace(s) ? "" : char.ToUpperInvariant(s.Trim()[0]).ToString();
        var a = Ini(p.Nombre); var b = Ini(p.Apellidos);
        var r = a + b;
        return r.Length > 0 ? r : "?";
    }
}

/// <summary>Un árbol abierto: su ruta, el historial de cambios y si hay cambios sin guardar.</summary>
public sealed class Documento
{
    private readonly List<Arbol> _deshacer = new();
    private readonly List<Arbol> _rehacer = new();
    private const int Limite = 100;

    public Arbol Arbol { get; private set; }
    public string? Ruta { get; set; }
    public bool Modificado { get; set; }

    public Documento(Arbol arbol, string? ruta = null) { Arbol = arbol; Ruta = ruta; }

    /// <summary>Nombre del árbol en la ventana: el del archivo (sin extensión); si aún no se ha guardado, el nombre interno.</summary>
    public string Titulo =>
        Ruta != null ? Path.GetFileNameWithoutExtension(Ruta)
        : !string.IsNullOrWhiteSpace(Arbol.Nombre) ? Arbol.Nombre : "Sin título";

    public bool PuedeDeshacer => _deshacer.Count > 0;
    public bool PuedeRehacer => _rehacer.Count > 0;

    /// <summary>Guarda el estado actual antes de modificarlo.</summary>
    public void Registrar()
    {
        _deshacer.Add(Arbol.Clonar());
        if (_deshacer.Count > Limite) _deshacer.RemoveAt(0);
        _rehacer.Clear();
        Modificado = true;
    }

    public bool Deshacer()
    {
        if (!PuedeDeshacer) return false;
        _rehacer.Add(Arbol);
        Arbol = _deshacer[^1]; _deshacer.RemoveAt(_deshacer.Count - 1);
        Modificado = true;
        return true;
    }

    /// <summary>Deshace sin dejar rastro para rehacer (p. ej. al cancelar el diálogo de una persona nueva).</summary>
    public void DescartarUltimo()
    {
        if (!PuedeDeshacer) return;
        Arbol = _deshacer[^1]; _deshacer.RemoveAt(_deshacer.Count - 1);
    }

    public bool Rehacer()
    {
        if (!PuedeRehacer) return false;
        _deshacer.Add(Arbol);
        Arbol = _rehacer[^1]; _rehacer.RemoveAt(_rehacer.Count - 1);
        Modificado = true;
        return true;
    }
}

public sealed class Preferencias
{
    public string? UltimoArchivo { get; set; }
    /// <summary>"A", "B", "C" o "D".</summary>
    public string? Ordenacion { get; set; }
    /// <summary>Posición y tamaño de la ventana al cerrarla.</summary>
    public PosicionVentana? Ventana { get; set; }
    /// <summary>Última vista de cada árbol (por ruta completa del archivo).</summary>
    public Dictionary<string, VistaGuardada> Vistas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private const int MaxVistas = 40;

    public VistaGuardada? VistaDe(string? ruta) => ruta != null && Vistas.TryGetValue(ruta, out var v) ? v : null;

    public void RecordarVista(string? ruta, VistaGuardada? vista)
    {
        if (ruta == null || vista == null) return;
        vista.Fecha = DateTime.Now;
        Vistas[ruta] = vista;
        foreach (var vieja in Vistas.OrderByDescending(kv => kv.Value.Fecha).Skip(MaxVistas).Select(kv => kv.Key).ToList())
            Vistas.Remove(vieja);
    }

    private static string Ruta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArbolGenealogico", "config.json");

    public static Preferencias Cargar()
    {
        try
        {
            var p = JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) ?? new();
            p.Vistas = new Dictionary<string, VistaGuardada>(p.Vistas ?? new(), StringComparer.OrdinalIgnoreCase);
            return p;
        }
        catch { return new(); }
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            File.WriteAllText(Ruta, JsonSerializer.Serialize(this));
        }
        catch { /* no es crítico */ }
    }
}

/// <summary>Vista de un árbol: el centro de la pantalla respecto a una persona, el zoom y la persona seleccionada.</summary>
public sealed class VistaGuardada
{
    public string Persona { get; set; } = "";
    public double Dx { get; set; }
    public double Dy { get; set; }
    public double Escala { get; set; } = 1;
    public string? Seleccion { get; set; }
    public DateTime Fecha { get; set; }
}

public sealed class PosicionVentana
{
    public double Izquierda { get; set; }
    public double Arriba { get; set; }
    public double Ancho { get; set; }
    public double Alto { get; set; }
    public bool Maximizada { get; set; }
}
