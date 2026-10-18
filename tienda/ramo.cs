#:package SkiaSharp@3.119.4
using SkiaSharp;

// Icono de «Ramo»: el árbol de la aplicación como un ramo de flores. Las flores son las tarjetas (azul, rosa, lila, gris),
// los tallos son las líneas del árbol (con las esquinas redondeadas) que se juntan en el nudo dorado de una pareja, que
// hace de lazo; por debajo asoman los tallos. Coordenadas en un lienzo de 512.
//   dotnet run ramo.cs -- muestra salida.png [variante]
//   dotnet run ramo.cs -- todo <Resources de Android> <icono 512 para app/web> <icono Play> <icono.ico>

var Azul = new SKColor(0x72, 0xA2, 0xFF);
var Rosa = new SKColor(0xF5, 0x84, 0xC0);
var Lila = new SKColor(0xB3, 0x9B, 0xFF);
var Gris = new SKColor(0xA3, 0xAA, 0xBE);
var Oro = new SKColor(0xF5, 0xC4, 0x51);
var Linea = new SKColor(0x7A, 0x86, 0xA0);
var Hoja = new SKColor(0x5E, 0xEA, 0xD4);
var Fondo = new SKColor(0x13, 0x16, 0x1F);

string variante = args[0] == "muestra" && args.Length > 2 ? args[2] : "b";

void Ramo(SKCanvas c, float cx, float cy, float s, bool mono = false)
{
    c.Save();
    c.Translate(cx, cy); c.Scale(s); c.Translate(-256, -256);
    using var p = new SKPaint { IsAntialias = true, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    SKColor C(SKColor col) => mono ? SKColors.White : col;

    // las flores: (centro, color, giro)
    var flores = new (float X, float Y, SKColor Col, float Giro)[]
    {
        (256, 118, Rosa, 0),
        (164, 160, Azul, -12), (348, 160, Azul, 12),
        (98, 236, Lila, -24), (414, 236, Gris, 24),
    };
    float w = 92, h = 66, r = 20;
    var nudo = new SKPoint(256, 362);

    // tallos: de cada flor baja una línea, gira con esquina redondeada y se junta con las demás en el nudo
    p.Style = SKPaintStyle.Stroke;
    foreach (var f in flores)
    {
        bool centro = f.X == 256;
        p.Color = C(centro ? Oro : Linea); p.StrokeWidth = centro ? 14 : 13;
        using var t = new SKPath();
        if (variante == "a")
        {
            // como en el árbol: vertical, horizontal y vertical, con esquinas redondeadas
            float y = f.Y + h / 2;
            float bus = centro ? 0 : (Math.Abs(f.X - 256) > 120 ? 306 : 276);
            t.MoveTo(f.X, y);
            if (centro) t.LineTo(256, nudo.Y);
            else
            {
                float sx = Math.Sign(256 - f.X), rr = 26;
                t.LineTo(f.X, bus - rr);
                t.QuadTo(f.X, bus, f.X + sx * rr, bus);
                t.LineTo(256 - sx * rr, bus);
                t.QuadTo(256, bus, 256, bus + rr);
                t.LineTo(256, nudo.Y);
            }
        }
        else if (variante == "b")
        {
            // en abanico: rectos hasta el nudo
            t.MoveTo(f.X, f.Y + h / 2 - 6); t.LineTo(nudo.X, nudo.Y);
        }
        else
        {
            // bajan rectos y se curvan hasta juntarse en el nudo, como un manojo de tallos
            t.MoveTo(f.X, f.Y);
            if (centro) t.LineTo(256, nudo.Y);
            else
            {
                float baja = f.Y + h / 2 + (Math.Abs(f.X - 256) > 120 ? 20 : 34);
                t.LineTo(f.X, baja);
                t.CubicTo(f.X, nudo.Y - 70, 256 + (f.X - 256) * 0.12f, nudo.Y - 40, 256, nudo.Y);
            }
        }
        c.DrawPath(t, p);
    }
    // por debajo del lazo asoman los tallos
    p.Color = C(Oro); p.StrokeWidth = 12;
    c.DrawLine(256, nudo.Y, 256, 452, p);
    c.DrawLine(256, nudo.Y, 222, 444, p);
    c.DrawLine(256, nudo.Y, 290, 444, p);
    // dos hojas junto a los tallos
    {
        p.Style = SKPaintStyle.Fill; p.Color = C(Hoja.WithAlpha(0xD0));
        foreach (var lado in new[] { -1f, 1f })
        {
            c.Save();
            c.Translate(256 + lado * 46, 322); c.RotateDegrees(lado * -38);
            using var hoja = new SKPath();
            hoja.MoveTo(0, -30); hoja.QuadTo(20, 0, 0, 30); hoja.QuadTo(-20, 0, 0, -30);
            c.DrawPath(hoja, p);
            c.Restore();
        }
    }
    // el lazo: el nudo de las parejas, un aro dorado
    p.Style = SKPaintStyle.Fill; p.Color = mono ? SKColors.Transparent : Fondo;
    if (!mono) c.DrawCircle(nudo, 17, p);
    p.Style = SKPaintStyle.Stroke; p.Color = C(Oro); p.StrokeWidth = 9;
    c.DrawCircle(nudo, 17, p);

    // las flores, encima de los tallos: las tarjetas
    p.Style = SKPaintStyle.Fill;
    foreach (var f in flores)
    {
        c.Save();
        c.Translate(f.X, f.Y); if (variante != "a") c.RotateDegrees(f.Giro);
        p.Color = C(f.Col);
        c.DrawRoundRect(new SKRect(-w / 2, -h / 2, w / 2, h / 2), r, r, p);
        c.Restore();
    }
    c.Restore();
}

SKBitmap Marco(int lado, bool aSangre)
{
    var b = new SKBitmap(lado, lado);
    using var c = new SKCanvas(b);
    c.Clear(SKColors.Transparent);
    float k = lado / 512f;
    using var p = new SKPaint { IsAntialias = true };
    if (aSangre) { c.Clear(Fondo); Ramo(c, lado / 2f, lado / 2f, 0.84f * k); return b; }
    // como el icono de antes: cuadrado redondeado oscuro con el borde dorado
    var caja = new SKRect(22 * k, 22 * k, 490 * k, 490 * k);
    p.Color = Fondo; c.DrawRoundRect(caja, 108 * k, 108 * k, p);
    p.Style = SKPaintStyle.Stroke; p.StrokeWidth = 14 * k; p.Color = Oro;
    c.DrawRoundRect(caja, 108 * k, 108 * k, p);
    Ramo(c, lado / 2f, lado / 2f + 4 * k, 0.78f * k);
    return b;
}

void Guardar(SKBitmap b, string ruta)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ruta))!);
    using var f = File.Create(ruta);
    b.Encode(f, SKEncodedImageFormat.Png, 100);
}

if (args[0] == "muestra")
{
    // las tres versiones juntas: con marco, a sangre (Play) y adaptativo (recortado en círculo) y pequeña
    using var b = new SKBitmap(1700, 560);
    using var c = new SKCanvas(b);
    c.Clear(new SKColor(0x30, 0x33, 0x3A));
    using (var m = Marco(512, false)) c.DrawBitmap(m, 20, 24);
    using (var m = Marco(512, true)) { c.Save(); c.ClipRoundRect(new SKRoundRect(new SKRect(560, 24, 1072, 536), 90, 90)); c.DrawBitmap(m, 560, 24); c.Restore(); }
    c.Save(); c.ClipPath(Circulo(1350, 280, 220)); c.Clear(Fondo); Ramo(c, 1350, 280, 0.72f * 440f / 512f * 1.5f / 1.5f); c.Restore();
    using (var m = Marco(48, false)) c.DrawBitmap(m, 1600, 60);
    using (var m = Marco(96, false)) c.DrawBitmap(m, 1580, 140);
    Guardar(b, args[1]);
    return;
}

static SKPath Circulo(float x, float y, float r) { var p = new SKPath(); p.AddCircle(x, y, r); return p; }

if (args[0] == "grande")
{
    // el ramo solo, sobre transparente, del lado pedido (para la imagen destacada)
    int lado = int.Parse(args[2]);
    using var b = new SKBitmap(lado, lado);
    using var c = new SKCanvas(b);
    c.Clear(SKColors.Transparent);
    Ramo(c, lado / 2f, lado / 2f, 0.56f * lado / 432f);
    Guardar(b, args[1]);
    return;
}

if (args[0] == "todo")
{
    string res = args[1], icono = args[2], play = args[3], ico = args[4];
    // 1. adaptativo: primer plano transparente con el ramo en la zona segura (66 de 108); monocromo aparte
    using (var b = new SKBitmap(432, 432))
    using (var c = new SKCanvas(b))
    {
        c.Clear(SKColors.Transparent);
        Ramo(c, 216, 216, 0.56f);
        Guardar(b, Path.Combine(res, "mipmap-xxxhdpi", "icono_frente.png"));
    }
    using (var b = new SKBitmap(432, 432))
    using (var c = new SKCanvas(b))
    {
        c.Clear(SKColors.Transparent);
        Ramo(c, 216, 216, 0.56f, mono: true);
        Guardar(b, Path.Combine(res, "mipmap-xxxhdpi", "icono_mono.png"));
    }
    // 2. clásicos por densidad y el de 512 (aplicación, web)
    using (var m = Marco(512, false)) Guardar(m, icono);
    foreach (var (carpeta, lado) in new[] { ("mipmap-mdpi", 48), ("mipmap-hdpi", 72), ("mipmap-xhdpi", 96), ("mipmap-xxhdpi", 144), ("mipmap-xxxhdpi", 192) })
        using (var m = Marco(lado, false)) Guardar(m, Path.Combine(res, carpeta, "icono.png"));
    // 3. Google Play: a sangre
    using (var m = Marco(512, true)) Guardar(m, play);
    // 4. .ico de Windows con varios tamaños (PNG dentro)
    var tamanos = new[] { 16, 24, 32, 48, 64, 128, 256 };
    var datos = tamanos.Select(t => { using var m = Marco(t, false); using var d = m.Encode(SKEncodedImageFormat.Png, 100); return d.ToArray(); }).ToList();
    using (var f = File.Create(ico))
    using (var w2 = new BinaryWriter(f))
    {
        w2.Write((short)0); w2.Write((short)1); w2.Write((short)tamanos.Length);
        int desplazamiento = 6 + 16 * tamanos.Length;
        for (int i = 0; i < tamanos.Length; i++)
        {
            int t = tamanos[i];
            w2.Write((byte)(t >= 256 ? 0 : t)); w2.Write((byte)(t >= 256 ? 0 : t)); w2.Write((byte)0); w2.Write((byte)0);
            w2.Write((short)1); w2.Write((short)32); w2.Write(datos[i].Length); w2.Write(desplazamiento);
            desplazamiento += datos[i].Length;
        }
        foreach (var d in datos) w2.Write(d);
    }
    Console.WriteLine("ok");
}
