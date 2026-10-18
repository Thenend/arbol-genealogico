#:package SkiaSharp@3.119.4
using SkiaSharp;

// Imagen destacada de Google Play (1024 × 500): el ramo del icono, el nombre y la frase, en cada idioma de la ficha.
//   dotnet run destacada-ramo.cs -- salida.png <carpeta de fuentes> <icono_frente.png (ramo sobre transparente)> [es|en|fr|it|de|pt]
// (las fuentes están en src/ArbolGenealogico.Movil/Recursos/Fuentes y el ramo en mipmap-xxxhdpi/icono_frente.png del proyecto de Android)
var salida = args[0];
var fuentes = args[1];
using var ramo = SKBitmap.Decode(args[2]);
var idioma = args.Length > 3 ? args[3] : "es";
var (frase, linea1, linea2) = idioma switch
{
    "en" => ("Your family tree, on your phone.", "No accounts, no internet: share it", "on WhatsApp just like a photo."),
    "fr" => ("L'arbre de votre famille.", "Sans compte ni internet : partagez-le", "sur WhatsApp comme une photo."),
    "it" => ("L'albero della tua famiglia.", "Senza account né internet: condividilo", "su WhatsApp come una foto."),
    "de" => ("Der Stammbaum deiner Familie.", "Ohne Konto, ohne Internet: Teile ihn", "per WhatsApp wie ein Foto."),
    "pt" => ("A árvore genealógica da sua família.", "Sem conta, sem internet: compartilhe", "pelo WhatsApp como uma foto."),
    _ => ("El árbol genealógico de tu familia.", "Sin cuentas, sin internet: compártelo", "por WhatsApp como una foto."),
};
using var b = new SKBitmap(1024, 500);
using var c = new SKCanvas(b);
using (var fondo = new SKPaint { Shader = SKShader.CreateRadialGradient(new SKPoint(260, 240), 720,
           new[] { new SKColor(0x1C, 0x22, 0x33), new SKColor(0x0A, 0x0C, 0x11) }, SKShaderTileMode.Clamp) })
    c.DrawRect(0, 0, 1024, 500, fondo);

// el ramo ocupa la mitad del primer plano del icono adaptativo: se amplía para que llene el alto
using (var img = SKImage.FromBitmap(ramo))
    c.DrawImage(img, SKRect.Create(ramo.Width, ramo.Height), SKRect.Create(250 - ramo.Width / 2f, 245 - ramo.Height / 2f, ramo.Width, ramo.Height),
        new SKSamplingOptions(SKCubicResampler.Mitchell));

using var negrita = SKTypeface.FromFile(Path.Combine(fuentes, "BarlowSemiCondensed-SemiBold.ttf"));
using var normal = SKTypeface.FromFile(Path.Combine(fuentes, "BarlowSemiCondensed-Medium.ttf"));
using var t = new SKPaint { IsAntialias = true, Color = SKColors.White };
using var f1 = new SKFont(negrita, 112);
c.DrawText("Ramo", 486, 210, SKTextAlign.Left, f1, t);
t.Color = new SKColor(0xF5, 0xC4, 0x51);
// las frases, con la letra más pequeña si en ese idioma no caben hasta el borde (con 24 px de margen)
SKFont Ajustada(float tam, params string[] textos)
{
    var f = new SKFont(normal, tam);
    while (textos.Max(x => f.MeasureText(x)) > 1024 - 490 - 24) f.Size -= 0.5f;
    return f;
}
using var f2 = Ajustada(36, frase);
c.DrawText(frase, 490, 278, SKTextAlign.Left, f2, t);
t.Color = new SKColor(0x9A, 0xA2, 0xB6);
using var f3 = Ajustada(32, linea1, linea2);
c.DrawText(linea1, 490, 342, SKTextAlign.Left, f3, t);
c.DrawText(linea2, 490, 382, SKTextAlign.Left, f3, t);

using var s = File.Create(salida);
b.Encode(s, SKEncodedImageFormat.Png, 100);
Console.WriteLine("ok");
