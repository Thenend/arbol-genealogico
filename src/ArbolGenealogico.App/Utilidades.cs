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

    /// <summary>Lee una imagen, la reduce a ≤320 px y la devuelve como JPEG en base64.</summary>
    public static string? Importar(string ruta)
    {
        var marco = BitmapFrame.Create(new Uri(ruta), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapSource fuente = marco;
        double f = Math.Min(1.0, (double)LadoMaximo / Math.Max(marco.PixelWidth, marco.PixelHeight));
        if (f < 1) fuente = new TransformedBitmap(marco, new ScaleTransform(f, f));
        if (fuente.Format != PixelFormats.Bgr24) fuente = new FormatConvertedBitmap(fuente, PixelFormats.Bgr24, null, 0);
        var enc = new JpegBitmapEncoder { QualityLevel = 85 };
        enc.Frames.Add(BitmapFrame.Create(fuente));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
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

    public string Titulo =>
        !string.IsNullOrWhiteSpace(Arbol.Nombre) ? Arbol.Nombre
        : Ruta != null ? Path.GetFileNameWithoutExtension(Ruta) : "Sin título";

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

    private static string Ruta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArbolGenealogico", "config.json");

    public static Preferencias Cargar()
    {
        try { return JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) ?? new(); }
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
