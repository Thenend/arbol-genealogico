#:package SkiaSharp@3.119.4
using SkiaSharp;

// Imagen destacada de Google Play (1024 × 500): el ramo del icono y el nombre.
//   dotnet run destacada-ramo.cs -- salida.png <carpeta de fuentes> <icono_frente.png (ramo sobre transparente)>
var salida = args[0];
var fuentes = args[1];
using var ramo = SKBitmap.Decode(args[2]);
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
using var f2 = new SKFont(normal, 36);
c.DrawText("El árbol genealógico de tu familia.", 490, 278, SKTextAlign.Left, f2, t);
t.Color = new SKColor(0x9A, 0xA2, 0xB6);
using var f3 = new SKFont(normal, 32);
c.DrawText("Sin cuentas, sin internet: compártelo", 490, 342, SKTextAlign.Left, f3, t);
c.DrawText("por WhatsApp como una foto.", 490, 382, SKTextAlign.Left, f3, t);

using var s = File.Create(salida);
b.Encode(s, SKEncodedImageFormat.Png, 100);
Console.WriteLine("ok");
