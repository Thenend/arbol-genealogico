using System.Drawing;
using System.Drawing.Drawing2D;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Tests;

/// <summary>Dibujo PNG de depuración del layout (no es el renderizado de la app).</summary>
#pragma warning disable CA1416
public static class Dibujo
{
    public static void Guardar(Arbol a, LayoutResult r, string ruta, double escala = 0.5)
    {
        int w = (int)(r.Ancho * escala) + 1, h = (int)(r.Alto * escala) + 1;
        using var bmp = new Bitmap(Math.Max(w, 10), Math.Max(h, 10));
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(24, 26, 32));
        g.ScaleTransform((float)escala, (float)escala);
        var o = r.Opciones;
        using var gris = new Pen(Color.FromArgb(150, 150, 160), 2);
        using var oro = new Pen(Color.Gold, 5);
        var colores = new Dictionary<string, Pen>();
        int k = 0;
        foreach (var c in r.Conexiones)
        {
            if (!colores.TryGetValue(c.UnionId, out var pen))
            {
                var col = Color.FromArgb(255, 60 + (k * 53) % 180, 80 + (k * 97) % 170, 80 + (k * 31) % 170);
                colores[c.UnionId] = pen = new Pen(col, 2); k++;
            }
            var p = c.Directa ? oro : pen;
            g.DrawLines(p, c.Puntos.Select(x => new PointF((float)x.X, (float)x.Y)).ToArray());
        }
        using var fuente = new Font("Segoe UI", 14, FontStyle.Bold);
        foreach (var c in r.TodasLasCartas)
        {
            var p = a.Obtener(c.Id);
            var rect = new RectangleF((float)(c.X - o.AnchoCarta / 2), (float)c.Y, (float)o.AnchoCarta, (float)o.AltoCarta);
            var col = p.Sexo switch { Sexo.Hombre => Color.FromArgb(40, 70, 120), Sexo.Mujer => Color.FromArgb(120, 50, 85), _ => Color.FromArgb(70, 70, 76) };
            using var br = new SolidBrush(col);
            g.FillRectangle(br, rect);
            if (c.Politica) { using var d = new Pen(Color.Gray, 2) { DashStyle = DashStyle.Dash }; g.DrawRectangle(d, rect.X, rect.Y, rect.Width, rect.Height); }
            if (c.EsCopia) { using var d = new Pen(Color.Orange, 3) { DashStyle = DashStyle.Dot }; g.DrawRectangle(d, rect.X, rect.Y, rect.Width, rect.Height); }
            g.DrawString(p.Nombre == "" ? c.Id : p.Nombre, fuente, Brushes.White, rect.X + 8, rect.Y + 8);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        bmp.Save(ruta);
    }
}
