using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.App;

/// <summary>
/// El árbol como imagen (PNG o JPG): entero o solo una zona, en el tema de la pantalla (oscuro), claro o en blanco y negro,
/// con el nombre del árbol encima si se quiere, y a la escala que se pida. Se dibuja por trozos y se va juntando, para que un
/// árbol grande a mucho tamaño no necesite un único dibujo gigante (que, además de la memoria, WPF podría no aguantar).
/// </summary>
public sealed class ImagenArbol
{
    /// <summary>Lo más grande que se deja hacer: 100 millones de píxeles (unos 400 MB de memoria mientras se prepara).</summary>
    public const double MaxPixeles = 100e6;
    /// <summary>Lo más largo de un lado (el JPG no admite más de 65 535).</summary>
    public const int MaxLado = 60000;
    /// <summary>Lado de cada trozo que se dibuja de una vez (px).</summary>
    private const int Trozo = 4096;

    private readonly VistaArbol _vista;
    private readonly string _titulo;

    public EstiloPapel Tema { get; }

    public ImagenArbol(Arbol arbol, Ordenacion ordenacion, FormaTarjeta tarjetas, EstiloPapel tema)
    {
        Tema = tema;
        _titulo = arbol.Nombre?.Trim() ?? "";
        _vista = new VistaArbol { Ordenacion = ordenacion, Tarjetas = tarjetas };
        _vista.PrepararParaImagen(tema);
        _vista.Cargar(arbol, false);
    }

    /// <summary>El árbol entero, con su margen (en píxeles del árbol, a escala 1).</summary>
    public Rect Entero => new(0, 0, _vista.Layout!.Ancho, _vista.Layout.Alto);

    /// <summary>Alto de la letra de los nombres a escala 1 (px).</summary>
    public double LetraNombre => _vista.LetraNombre;

    public bool TieneTitulo => _titulo.Length > 0;

    /// <summary>Letra del título: algo más del doble que la de los nombres.</summary>
    private double LetraTitulo => 2.4 * LetraNombre;

    /// <summary>La franja de arriba para el título (0 sin título). Debajo queda aún el margen del árbol.</summary>
    private double Franja(bool titulo) => titulo && TieneTitulo ? 1.7 * LetraTitulo : 0;

    /// <summary>Lo que mide la imagen a escala 1: la zona y, encima, la franja del título.</summary>
    public Size Tamano(Rect zona, bool titulo) => new(zona.Width, zona.Height + Franja(titulo));

    /// <summary>Píxeles de la imagen a esa escala.</summary>
    public static (int Ancho, int Alto) Pixeles(Size tamano, double escala) =>
        (Math.Max(1, (int)Math.Round(tamano.Width * escala)), Math.Max(1, (int)Math.Round(tamano.Height * escala)));

    /// <summary>Si se puede hacer una imagen de esos píxeles (ni demasiado grande en total ni demasiado larga).</summary>
    public static bool Cabe(int ancho, int alto) => (double)ancho * alto <= MaxPixeles && ancho <= MaxLado && alto <= MaxLado;

    /// <summary>
    /// Dibuja la zona del árbol (coordenadas del árbol) a esa escala. Con <paramref name="transparente"/>, sin fondo: solo
    /// las tarjetas, los nombres y las líneas.
    /// </summary>
    public BitmapSource Dibujar(Rect zona, bool titulo, double escala, bool transparente)
    {
        var (w, h) = Pixeles(Tamano(zona, titulo), escala);
        double franja = Franja(titulo);
        int stride = w * 4;
        var pixeles = new byte[(long)stride * h];
        var trozo = new byte[Math.Min(Trozo, w) * 4 * Math.Min(Trozo, h)];
        var lienzo = new Canvas { ClipToBounds = true };
        lienzo.Children.Add(_vista);
        TextBlock? rotulo = null;
        if (franja > 0)
        {
            rotulo = new TextBlock
            {
                Text = _titulo,
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                FontSize = LetraTitulo * escala,
                Foreground = new SolidColorBrush(Tema switch
                {
                    EstiloPapel.Oscuro => Color.FromRgb(0xE6, 0xE9, 0xF0),
                    EstiloPapel.Claro => Color.FromRgb(0x1E, 0x25, 0x33),
                    _ => Colors.Black,
                }),
            };
            lienzo.Children.Add(rotulo);
            rotulo.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        try
        {
            for (int y0 = 0; y0 < h; y0 += Trozo)
                for (int x0 = 0; x0 < w; x0 += Trozo)
                {
                    int tw = Math.Min(Trozo, w - x0), th = Math.Min(Trozo, h - y0);
                    lienzo.Width = tw; lienzo.Height = th;
                    lienzo.Background = Fondo(transparente, w, h, x0, y0);
                    // El árbol, de forma que la esquina de la zona caiga en la esquina de la imagen (debajo de la franja).
                    _vista.Width = tw; _vista.Height = th;
                    _vista.Escala = escala;
                    _vista.Tx = -zona.X * escala - x0;
                    _vista.Ty = (franja - zona.Y) * escala - y0;
                    if (rotulo != null)
                    {
                        // Centrado sobre la imagen, en la franja.
                        Canvas.SetLeft(rotulo, w / 2.0 - rotulo.DesiredSize.Width / 2 - x0);
                        Canvas.SetTop(rotulo, franja * escala * 0.62 - rotulo.DesiredSize.Height / 2 - y0);
                    }
                    lienzo.Measure(new Size(tw, th));
                    lienzo.Arrange(new Rect(0, 0, tw, th));
                    lienzo.UpdateLayout();
                    var rtb = new RenderTargetBitmap(tw, th, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(lienzo);
                    rtb.CopyPixels(trozo, tw * 4, 0);
                    for (int f = 0; f < th; f++)
                        Buffer.BlockCopy(trozo, f * tw * 4, pixeles, (int)((long)(y0 + f) * stride + x0 * 4), tw * 4);
                }
        }
        finally { lienzo.Children.Clear(); }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixeles, stride);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>
    /// El fondo de un trozo: nada (transparente), blanco (claro y blanco y negro) o el degradado de la pantalla (oscuro),
    /// puesto en coordenadas de la imagen entera para que siga sin cortes de un trozo a otro.
    /// </summary>
    private Brush? Fondo(bool transparente, int w, int h, int x0, int y0)
    {
        if (transparente) return null;
        if (Tema != EstiloPapel.Oscuro) return Brushes.White;
        var centro = new Point(w * 0.5 - x0, h * 0.45 - y0);
        return new RadialGradientBrush(Color.FromRgb(0x16, 0x1A, 0x26), Color.FromRgb(0x0A, 0x0C, 0x11))
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = centro, GradientOrigin = centro,
            RadiusX = 0.9 * w, RadiusY = 0.9 * h,
        };
    }

    /// <summary>Guarda la imagen como PNG o, si la ruta acaba en .jpg o .jpeg, como JPG (siempre con fondo).</summary>
    public static void Guardar(BitmapSource imagen, string ruta, bool transparente)
    {
        bool jpg = ruta.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || ruta.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
        // Sin transparencia no hace falta el canal alfa: el archivo pesa menos.
        var formato = jpg || !transparente ? PixelFormats.Bgr24 : PixelFormats.Bgra32;
        BitmapEncoder enc = jpg ? new JpegBitmapEncoder { QualityLevel = 92 } : new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(imagen, formato, null, 0)));
        // Primero a un archivo temporal: si algo falla, no queda una imagen a medias (ni se pierde la que hubiera).
        var temporal = ruta + ".tmp";
        try
        {
            using (var fs = File.Create(temporal)) enc.Save(fs);
            File.Move(temporal, ruta, true);
        }
        finally { if (File.Exists(temporal)) File.Delete(temporal); }
    }
}
