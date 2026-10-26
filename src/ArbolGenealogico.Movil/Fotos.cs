using Avalonia.Media.Imaging;
using SkiaSharp;

namespace ArbolGenealogico.Movil;

/// <summary>Fotos de las personas: van dentro del árbol, en JPEG y base64, reducidas a 320 px como en el escritorio.</summary>
public static class Fotos
{
    public const int Lado = 320;

    private static readonly Dictionary<string, Bitmap?> _cache = new();

    /// <summary>La foto lista para dibujar (se recuerda: decodificarla cuesta).</summary>
    public static Bitmap? Imagen(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        if (_cache.TryGetValue(base64, out var b)) return b;
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(base64));
            b = new Bitmap(ms);
        }
        catch { b = null; }
        if (_cache.Count > 600) _cache.Clear();
        _cache[base64] = b;
        return b;
    }

    private static readonly Dictionary<string, Bitmap?> _grises = new();

    /// <summary>La foto en grises, como sale con tinta negra (para imprimir en blanco y negro).</summary>
    public static Bitmap? ImagenGris(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        if (_grises.TryGetValue(base64, out var b)) return b;
        try
        {
            using var color = SKBitmap.Decode(Convert.FromBase64String(base64));
            using var gris = new SKBitmap(color.Width, color.Height);
            using (var lienzo = new SKCanvas(gris))
            using (var pintura = new SKPaint())
            {
                // el brillo de cada punto, con el peso de cada color como lo ve el ojo
                pintura.ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
                {
                    0.2126f, 0.7152f, 0.0722f, 0, 0,
                    0.2126f, 0.7152f, 0.0722f, 0, 0,
                    0.2126f, 0.7152f, 0.0722f, 0, 0,
                    0, 0, 0, 1, 0,
                });
                lienzo.DrawBitmap(color, 0, 0, pintura);
            }
            using var datos = gris.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(datos.ToArray());
            b = new Bitmap(ms);
        }
        catch { b = null; }
        if (_grises.Count > 600) _grises.Clear();
        _grises[base64] = b;
        return b;
    }

    /// <summary>Lo más grande que se guarda en memoria de una foto mientras se encuadra (las de la cámara son enormes).</summary>
    private const int LadoMaximoAlEncuadrar = 2048;

    /// <summary>
    /// Una imagen cualquiera (de la galería o de la cámara) derecha según su orientación (las de móvil suelen venir
    /// giradas) y reducida a un tamaño manejable, para encuadrarla. Null si no es una imagen que se pueda leer.
    /// </summary>
    public static SKBitmap? Abrir(byte[] datos)
    {
        using var codec = SKCodec.Create(new MemoryStream(datos));
        if (codec == null) return null;
        using var original = SKBitmap.Decode(codec);
        if (original == null) return null;
        var derecha = Enderezar(original, codec.EncodedOrigin);
        double k = Math.Min(1, (double)LadoMaximoAlEncuadrar / Math.Max(derecha.Width, derecha.Height));
        if (k >= 1) return derecha;
        using (derecha)
            return derecha.Resize(new SKImageInfo((int)Math.Round(derecha.Width * k), (int)Math.Round(derecha.Height * k)),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    /// <summary>La foto ya guardada de una persona, para volver a encuadrarla.</summary>
    public static SKBitmap? Abrir(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try { return Abrir(Convert.FromBase64String(base64)); }
        catch (FormatException) { return null; }
    }

    /// <summary>El cuadrado del centro de la imagen, lo más grande posible (el encuadre de partida).</summary>
    public static SKRect Centrado(SKBitmap img)
    {
        float lado = Math.Min(img.Width, img.Height);
        return SKRect.Create((img.Width - lado) / 2, (img.Height - lado) / 2, lado, lado);
    }

    /// <summary>
    /// Guarda en <paramref name="ruta"/> una foto para el fondo de la lámina: derecha y reducida como al encuadrar (sale
    /// tenue, así que no necesita más), en JPEG. False si no es una imagen que se pueda leer.
    /// </summary>
    public static bool GuardarFondo(byte[] datos, string ruta)
    {
        using var img = Abrir(datos);
        if (img == null) return false;
        using var jpg = img.Encode(SKEncodedImageFormat.Jpeg, 88);
        File.WriteAllBytes(ruta, jpg.ToArray());
        return true;
    }

    /// <summary>La foto que se guarda: el cuadrado elegido de la imagen, reducido a 320 px, en JPEG y base64.</summary>
    public static string Recortar(SKBitmap img, SKRect cuadrado)
    {
        int destino = Math.Max(1, (int)Math.Min(Lado, Math.Round(cuadrado.Width)));
        using var final = new SKBitmap(destino, destino);
        using (var lienzo = new SKCanvas(final))
        {
            using var imagen = SKImage.FromBitmap(img);
            lienzo.DrawImage(imagen, cuadrado, SKRect.Create(destino, destino), new SKSamplingOptions(SKCubicResampler.Mitchell));
        }
        using var jpg = final.Encode(SKEncodedImageFormat.Jpeg, 86);
        return Convert.ToBase64String(jpg.ToArray());
    }

    private static SKBitmap Enderezar(SKBitmap b, SKEncodedOrigin origen)
    {
        if (origen == SKEncodedOrigin.TopLeft) return b.Copy();
        bool girada = origen is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var r = new SKBitmap(girada ? b.Height : b.Width, girada ? b.Width : b.Height);
        using var c = new SKCanvas(r);
        switch (origen)
        {
            case SKEncodedOrigin.TopRight: c.Scale(-1, 1, b.Width / 2f, 0); break;
            case SKEncodedOrigin.BottomRight: c.RotateDegrees(180, b.Width / 2f, b.Height / 2f); break;
            case SKEncodedOrigin.BottomLeft: c.Scale(1, -1, 0, b.Height / 2f); break;
            // (los puntos pasan por las transformaciones de la última a la primera)
            case SKEncodedOrigin.LeftTop: c.RotateDegrees(90); c.Scale(1, -1); break;                                 // (x,y) → (y,x)
            case SKEncodedOrigin.RightTop: c.Translate(r.Width, 0); c.RotateDegrees(90); break;                      // 90° a la derecha
            case SKEncodedOrigin.RightBottom: c.Translate(r.Width, r.Height); c.RotateDegrees(-90); c.Scale(1, -1); break;
            case SKEncodedOrigin.LeftBottom: c.Translate(0, r.Height); c.RotateDegrees(-90); break;
        }
        c.DrawBitmap(b, 0, 0);
        return r;
    }
}
