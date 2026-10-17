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

    /// <summary>
    /// Una imagen cualquiera (de la galería o de la cámara) convertida a la foto que se guarda: derecha según su
    /// orientación (las de móvil suelen venir giradas), recortada al centro en cuadrado y reducida a 320 px.
    /// </summary>
    public static string? Preparar(byte[] datos)
    {
        using var codec = SKCodec.Create(new MemoryStream(datos));
        if (codec == null) return null;
        using var original = SKBitmap.Decode(codec);
        if (original == null) return null;
        using var derecha = Enderezar(original, codec.EncodedOrigin);

        int lado = Math.Min(derecha.Width, derecha.Height);
        var recorte = SKRectI.Create((derecha.Width - lado) / 2, (derecha.Height - lado) / 2, lado, lado);
        int destino = Math.Min(Lado, lado);
        using var final = new SKBitmap(destino, destino);
        using (var lienzo = new SKCanvas(final))
        {
            using var img = SKImage.FromBitmap(derecha);
            lienzo.DrawImage(img, recorte, SKRect.Create(destino, destino), new SKSamplingOptions(SKCubicResampler.Mitchell));
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
