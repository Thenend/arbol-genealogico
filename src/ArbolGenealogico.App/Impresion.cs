using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.App;

/// <summary>
/// Imprime el árbol repartido en las hojas de la guía de impresión, listas para pegar: cada hoja lleva su trozo del árbol
/// dentro de un margen de <see cref="GuiaImpresion.MargenMm"/> mm; las que van a la derecha o debajo de otra llevan una línea
/// de corte por la que se recorta su margen, y la de al lado lleva en ese margen la zona donde se pega, con la línea en la
/// que apoyar el borde recortado. Así el dibujo sigue sin cortes de una hoja a otra.
/// </summary>
public static class Impresion
{
    private const double DipPorMm = 96 / 25.4;
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Abre el diálogo de impresión con el papel y la orientación de la configuración, e imprime una página por hoja.</summary>
    public static void Imprimir(Window propietario, Arbol arbol, Ordenacion ordenacion, FormaTarjeta tarjetas, EstiloPapel estilo, GuiaImpresion.Configuracion c)
    {
        var dlg = new PrintDialog { UserPageRangeEnabled = false };
        try
        {
            dlg.PrintTicket.PageMediaSize = new PageMediaSize(c.Papel switch
            {
                "A1" => PageMediaSizeName.ISOA1, "A2" => PageMediaSizeName.ISOA2, "A3" => PageMediaSizeName.ISOA3, _ => PageMediaSizeName.ISOA4,
            });
            dlg.PrintTicket.PageOrientation = c.Vertical ? PageOrientation.Portrait : PageOrientation.Landscape;
        }
        catch { /* sin impresoras o un controlador que no lo admite: se usa lo que tenga */ }
        if (dlg.ShowDialog() != true) return;
        var doc = Documento(arbol, ordenacion, tarjetas, estilo, c);
        dlg.PrintDocument(doc.DocumentPaginator, $"{arbol.Nombre} ({c.Hojas} hoja{(c.Hojas == 1 ? "" : "s")} {c.Papel})");
    }

    /// <summary>Las hojas, en orden (por filas, de izquierda a derecha).</summary>
    public static FixedDocument Documento(Arbol arbol, Ordenacion ordenacion, FormaTarjeta tarjetas, EstiloPapel estilo, GuiaImpresion.Configuracion c)
    {
        double anchoPag = c.AnchoHojaMm * DipPorMm, altoPag = c.AltoHojaMm * DipPorMm;
        var doc = new FixedDocument();
        doc.DocumentPaginator.PageSize = new Size(anchoPag, altoPag);
        for (int f = 0; f < c.Filas; f++)
            for (int col = 0; col < c.Columnas; col++)
            {
                var pagina = Pagina(arbol, ordenacion, tarjetas, estilo, c, f, col);
                var contenido = new PageContent();
                ((System.Windows.Markup.IAddChild)contenido).AddChild(pagina);
                doc.Pages.Add(contenido);
            }
        return doc;
    }

    private static FixedPage Pagina(Arbol arbol, Ordenacion ordenacion, FormaTarjeta tarjetas, EstiloPapel estilo, GuiaImpresion.Configuracion c, int f, int col)
    {
        double anchoPag = c.AnchoHojaMm * DipPorMm, altoPag = c.AltoHojaMm * DipPorMm, m = GuiaImpresion.MargenMm * DipPorMm;
        var pagina = new FixedPage { Width = anchoPag, Height = altoPag, Background = Brushes.White };

        // El trozo del árbol, dentro del margen.
        var pieza = c.Pieza(f, col);
        double s = c.MmPorPx * DipPorMm;                 // DIP por píxel del árbol
        var vista = new VistaArbol { Ordenacion = ordenacion, Tarjetas = tarjetas, Width = pieza.Width * s, Height = pieza.Height * s };
        vista.PrepararParaImprimir(VistaArbol.EstiloEfectivo(estilo, tarjetas));
        vista.Cargar(arbol, false);
        vista.Escala = s; vista.Tx = -pieza.X * s; vista.Ty = -pieza.Y * s;
        FixedPage.SetLeft(vista, m); FixedPage.SetTop(vista, m);
        pagina.Children.Add(vista);

        // Marcas para recortar y pegar.
        pagina.Children.Add(new Marcas(c, f, col, pieza.Width * s, pieza.Height * s) { Width = anchoPag, Height = altoPag });

        pagina.Measure(new Size(anchoPag, altoPag));
        pagina.Arrange(new Rect(0, 0, anchoPag, altoPag));
        pagina.UpdateLayout();
        return pagina;
    }

    /// <summary>Número de una hoja (1, 2...) contando por filas.</summary>
    private static int Numero(GuiaImpresion.Configuracion c, int f, int col) => f * c.Columnas + col + 1;

    /// <summary>Líneas de corte, zonas de pegado y rótulo de una hoja.</summary>
    private sealed class Marcas : FrameworkElement
    {
        private readonly GuiaImpresion.Configuracion _c;
        private readonly int _f, _col;
        private readonly double _anchoPieza, _altoPieza;   // lo que ocupa el trozo del árbol (DIP)

        public Marcas(GuiaImpresion.Configuracion c, int f, int col, double anchoPieza, double altoPieza)
        {
            _c = c; _f = f; _col = col; _anchoPieza = anchoPieza; _altoPieza = altoPieza; IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth > 0 ? ActualWidth : Width, h = ActualHeight > 0 ? ActualHeight : Height;
            double m = GuiaImpresion.MargenMm * DipPorMm, mm = DipPorMm;
            var gris = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));
            var corte = new Pen(gris, 0.8) { DashStyle = new DashStyle(new double[] { 6, 4 }, 0) };
            var apoyo = new Pen(new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)), 0.6);
            var zona = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
            int n = Numero(_c, _f, _col);

            FormattedText Texto(string t, double tam) => new(t, Es, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), tam, gris, 1.0);

            // Recortar: el margen de la izquierda (si hay una hoja a su izquierda) y el de arriba (si hay una encima).
            if (_col > 0)
            {
                dc.DrawLine(corte, new Point(m, 0), new Point(m, h));
                var t = Texto($"✂  Recortar por la línea y pegar sobre la hoja {n - 1}", 2.6 * mm);
                dc.PushTransform(new RotateTransform(-90, m * 0.55, h / 2));
                dc.DrawText(t, new Point(m * 0.55 - t.Width / 2, h / 2 - t.Height / 2));
                dc.Pop();
            }
            if (_f > 0)
            {
                dc.DrawLine(corte, new Point(0, m), new Point(w, m));
                var t = Texto($"✂  Recortar por la línea y pegar sobre la hoja {n - _c.Columnas}", 2.6 * mm);
                dc.DrawText(t, new Point(w / 2 - t.Width / 2, m * 0.55 - t.Height / 2));
            }

            // Pegar: el margen de la derecha (si hay una hoja a su derecha) y el de abajo (si hay una debajo), con la línea en la
            // que apoyar el borde recortado de la otra hoja.
            // El trozo del árbol puede ser más pequeño que la zona imprimible (los cortes buscan huecos entre tarjetas):
            // entonces la zona de pegado empieza donde acaba el trozo.
            double finX = m + _anchoPieza, finY = m + _altoPieza;
            if (_col < _c.Columnas - 1)
            {
                dc.DrawRectangle(zona, null, new Rect(finX, 0, w - finX, h));
                dc.DrawLine(apoyo, new Point(finX, 0), new Point(finX, h));
                var t = Texto($"Pegar aquí la hoja {n + 1}, con su borde recortado sobre esta línea", 2.6 * mm);
                double cx = w - m * 0.5;
                dc.PushTransform(new RotateTransform(90, cx, h / 2));
                dc.DrawText(t, new Point(cx - t.Width / 2, h / 2 - t.Height / 2));
                dc.Pop();
            }
            if (_f < _c.Filas - 1)
            {
                dc.DrawRectangle(zona, null, new Rect(0, finY, w, h - finY));
                dc.DrawLine(apoyo, new Point(0, finY), new Point(w, finY));
                var t = Texto($"Pegar aquí la hoja {n + _c.Columnas}, con su borde recortado sobre esta línea", 2.6 * mm);
                dc.DrawText(t, new Point(m + 4 * mm, h - m * 0.5 - t.Height / 2));
            }

            // Rótulo de la hoja, en el margen de abajo, a la derecha (nunca en una parte que se recorta).
            if (_c.Hojas > 1)
            {
                string donde = _c.Filas > 1 && _c.Columnas > 1 ? $" (fila {_f + 1}, columna {_col + 1})" : "";
                var t = Texto($"Hoja {n} de {_c.Hojas}{donde}", 2.6 * mm);
                double x = (_col < _c.Columnas - 1 ? w - m : w) - t.Width - 4 * mm;
                dc.DrawText(t, new Point(x, h - m * 0.5 - t.Height / 2));
            }
        }
    }

    /// <summary>Para pruebas: guarda cada hoja como PNG (hoja1.png, hoja2.png...) en la carpeta indicada.</summary>
    public static void GuardarPng(Arbol arbol, Ordenacion ordenacion, FormaTarjeta tarjetas, EstiloPapel estilo, GuiaImpresion.Configuracion c, string carpeta, double dpi = 60)
    {
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "info.txt"),
            $"{c.Papel} {c.Filas}x{c.Columnas} {(c.Vertical ? "vertical" : "horizontal")}  mm/px={c.MmPorPx.ToString(CultureInfo.InvariantCulture)}  tarjetas cortadas={c.TarjetasCortadas}");
        var doc = Documento(arbol, ordenacion, tarjetas, estilo, c);
        int i = 1;
        foreach (var pc in doc.Pages)
        {
            var p = pc.Child;
            double k = dpi / 96;
            var rtb = new RenderTargetBitmap((int)(p.Width * k), (int)(p.Height * k), dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(p);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(Path.Combine(carpeta, $"hoja{i++}.png"));
            enc.Save(fs);
        }
    }
}
