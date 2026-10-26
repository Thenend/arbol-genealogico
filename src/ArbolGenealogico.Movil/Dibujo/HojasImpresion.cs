using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ArbolGenealogico.Core.Presentacion;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Dibujo;

/// <summary>
/// El árbol repartido en las hojas de un <see cref="PlanImpresion.Configuracion"/>, listas para pegar (como al imprimir en
/// el escritorio): cada hoja lleva su trozo del árbol dentro de un margen de <see cref="PlanImpresion.MargenMm"/> mm; las que
/// van a la derecha o debajo de otra llevan una línea de corte por la que se recorta su margen, y la de al lado lleva en ese
/// margen la zona donde se pega, con la línea en la que apoyar el borde recortado. Así el dibujo sigue de una hoja a otra.
/// </summary>
public static class HojasImpresion
{
    /// <summary>Puntos de PDF (1/72 de pulgada) por milímetro.</summary>
    private const double PtPorMm = 72 / 25.4;
    private static readonly Typeface Letra = new(Estilo.Letra, FontStyle.Normal, FontWeight.Medium);
    private static readonly IBrush Gris = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));

    /// <summary>
    /// Guarda las hojas en un PDF, una página por hoja (por filas, de izquierda a derecha), del tamaño del papel.
    /// <paramref name="marca"/>: con «Hecho con Ramo» en el margen de arriba de la hoja de arriba a la derecha (la versión
    /// gratuita); ese margen no se recorta, así que queda en el árbol ya pegado.
    /// </summary>
    public static async Task GuardarPdf(LienzoArbol lienzo, PlanImpresion.Configuracion c, string ruta, string titulo, bool marca = false)
    {
        await using var s = File.Create(ruta);
        using var doc = SkiaSharp.SKDocument.CreatePdf(s, new SkiaSharp.SKDocumentPdfMetadata
        {
            Title = titulo, Creator = "Ramo", Producer = "Ramo", Creation = DateTime.Now, Modified = DateTime.Now,
        });
        double ancho = c.AnchoHojaMm * PtPorMm, alto = c.AltoHojaMm * PtPorMm;
        var pagina = new Rect(0, 0, ancho, alto);
        for (int f = 0; f < c.Filas; f++)
            for (int col = 0; col < c.Columnas; col++)
            {
                var hoja = new Hoja(lienzo, c, f, col, marca && f == 0 && col == c.Columnas - 1) { Width = ancho, Height = alto };
                hoja.Measure(pagina.Size);
                hoja.Arrange(pagina);
                var canvas = doc.BeginPage((float)ancho, (float)alto);
                await Avalonia.Skia.Helpers.DrawingContextHelper.RenderAsync(canvas, hoja, pagina, new Vector(96, 96));
                doc.EndPage();
            }
        doc.Close();
    }

    /// <summary>Número de una hoja (1, 2...) contando por filas.</summary>
    private static int Numero(PlanImpresion.Configuracion c, int f, int col) => f * c.Columnas + col + 1;

    /// <summary>Una hoja: papel blanco, su trozo del árbol y las marcas para recortar y pegar (en puntos de PDF).</summary>
    private sealed class Hoja(LienzoArbol lienzo, PlanImpresion.Configuracion c, int f, int col, bool marca) : Control
    {
        public override void Render(DrawingContext ctx)
        {
            double w = Width, h = Height, m = PlanImpresion.MargenMm * PtPorMm, mm = PtPorMm;
            ctx.FillRectangle(Brushes.White, new Rect(0, 0, w, h));

            // El trozo del árbol, dentro del margen y sin salirse de él (lo de al lado va en la otra hoja).
            var pieza = c.Pieza(f, col);
            double s = c.MmPorPx * PtPorMm;
            double anchoPieza = pieza.Ancho * s, altoPieza = pieza.Alto * s;
            using (ctx.PushClip(new Rect(m, m, anchoPieza, altoPieza)))
            using (ctx.PushTransform(Matrix.CreateTranslation(-pieza.X, -pieza.Y) * Matrix.CreateScale(s, s) * Matrix.CreateTranslation(m, m)))
                lienzo.DibujarEnPapel(ctx, new Rect(pieza.X, pieza.Y, pieza.Ancho, pieza.Alto));

            var corte = new Pen(Gris, 0.8) { DashStyle = new DashStyle(new double[] { 6, 4 }, 0) };
            var apoyo = new Pen(new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)), 0.6);
            var zona = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
            int n = Numero(c, f, col);
            FormattedText Texto(string t) => new(t, Cultura, FlowDirection.LeftToRight, Letra, 2.6 * mm, Gris);

            // Recortar: el margen de la izquierda (si hay una hoja a su izquierda) y el de arriba (si hay una encima).
            if (col > 0)
            {
                ctx.DrawLine(corte, new Point(m, 0), new Point(m, h));
                var t = Texto(T("Recortar por la línea y pegar sobre la hoja {0}", n - 1));
                using (ctx.PushTransform(Girar(-90, m * 0.55, h / 2)))
                    ctx.DrawText(t, new Point(m * 0.55 - t.Width / 2, h / 2 - t.Height / 2));
            }
            if (f > 0)
            {
                ctx.DrawLine(corte, new Point(0, m), new Point(w, m));
                var t = Texto(T("Recortar por la línea y pegar sobre la hoja {0}", n - c.Columnas));
                ctx.DrawText(t, new Point(w / 2 - t.Width / 2, m * 0.55 - t.Height / 2));
            }

            // Pegar: el margen de la derecha (si hay una hoja a su derecha) y el de abajo (si hay una debajo), con la línea en la
            // que apoyar el borde recortado de la otra hoja. El trozo del árbol puede ser más pequeño que la zona imprimible
            // (los cortes buscan huecos entre tarjetas): entonces la zona de pegado empieza donde acaba el trozo.
            double finX = m + anchoPieza, finY = m + altoPieza;
            if (col < c.Columnas - 1)
            {
                ctx.FillRectangle(zona, new Rect(finX, 0, w - finX, h));
                ctx.DrawLine(apoyo, new Point(finX, 0), new Point(finX, h));
                var t = Texto(T("Pegar aquí la hoja {0}, con su borde recortado sobre esta línea", n + 1));
                double cx = w - m * 0.5;
                using (ctx.PushTransform(Girar(90, cx, h / 2)))
                    ctx.DrawText(t, new Point(cx - t.Width / 2, h / 2 - t.Height / 2));
            }
            if (f < c.Filas - 1)
            {
                ctx.FillRectangle(zona, new Rect(0, finY, w, h - finY));
                ctx.DrawLine(apoyo, new Point(0, finY), new Point(w, finY));
                var t = Texto(T("Pegar aquí la hoja {0}, con su borde recortado sobre esta línea", n + c.Columnas));
                ctx.DrawText(t, new Point(m + 4 * mm, h - m * 0.5 - t.Height / 2));
            }

            if (marca)
            {
                var t = Texto(T("Hecho con Ramo"));
                ctx.DrawText(t, new Point(finX - t.Width, m * 0.5 - t.Height / 2));
            }

            // Rótulo de la hoja, en el margen de abajo, a la derecha (nunca en una parte que se recorta).
            if (c.Hojas > 1)
            {
                string texto = c.Filas > 1 && c.Columnas > 1
                    ? T("Hoja {0} de {1} (fila {2}, columna {3})", n, c.Hojas, f + 1, col + 1)
                    : T("Hoja {0} de {1}", n, c.Hojas);
                var t = Texto(texto);
                double x = (col < c.Columnas - 1 ? w - m : w) - t.Width - 4 * mm;
                ctx.DrawText(t, new Point(x, h - m * 0.5 - t.Height / 2));
            }
        }

        /// <summary>Un giro de <paramref name="grados"/> alrededor de un punto.</summary>
        private static Matrix Girar(double grados, double x, double y) =>
            Matrix.CreateTranslation(-x, -y) * Matrix.CreateRotation(grados * Math.PI / 180) * Matrix.CreateTranslation(x, y);
    }
}

/// <summary>
/// La vista previa de la impresión: las hojas ya pegadas, en blanco sobre la mesa, con el árbol como saldrá y las líneas
/// por donde se juntan las hojas. Se amplía con dos dedos (o la rueda del ratón) y se mueve con uno, para ver de cerca cómo
/// saldrán las tarjetas; dos toques seguidos alternan entre ver todas las hojas y verlas de cerca.
/// </summary>
public sealed class VistaHojas : VistaAmpliable
{
    private LienzoArbol? _lienzo;
    private PlanImpresion.Configuracion? _plan;
    private static readonly Color Azul = Color.FromRgb(0x2F, 0x7E, 0xD8);
    private static readonly Typeface LetraMarca = new(Estilo.Letra, FontStyle.Normal, FontWeight.Medium);
    private static readonly IBrush GrisMarca = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));
    private bool _marca;

    /// <summary>Con la marca «Hecho con Ramo» (la versión gratuita), donde saldrá en las hojas.</summary>
    public bool Marca
    {
        get => _marca;
        set { _marca = value; InvalidateVisual(); }
    }
    /// <summary>Lo más cerca que se puede ver: las tarjetas al doble de su tamaño en el árbol.</summary>
    protected override double EscalaMax => 2;
    /// <summary>La escala del doble toque: las tarjetas a un tamaño en el que se leen bien en el teléfono.</summary>
    protected override double EscalaCerca => 0.4;

    protected override Rect? Papel => _plan is { } c ? new Rect(c.Pegado.X, c.Pegado.Y, c.Pegado.Ancho, c.Pegado.Alto) : null;

    public void Mostrar(LienzoArbol lienzo, PlanImpresion.Configuracion? plan)
    {
        _lienzo = lienzo; _plan = plan;
        Rehecho();
    }

    // ---------- Dibujo ----------

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_lienzo == null || _plan is not { } c) return;
        double k = K, ox = Ox, oy = Oy;
        Rect APantalla(Caja r) => new(r.X * k + ox, r.Y * k + oy, r.Ancho * k, r.Alto * k);
        var papel = APantalla(c.Pegado);

        // el papel, con una sombra suave
        for (int i = 3; i >= 1; i--)
            ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)), papel.Translate(new Vector(i * 1.2, i * 2)).Inflate(i));
        ctx.FillRectangle(Brushes.White, papel);

        // el árbol como saldrá, solo lo que se ve (de lejos, los textos como barras: tan pequeños no se leerían y harían ir
        // lento el dibujo)
        var util = c.Util;
        var visible = new Rect(-ox / k, -oy / k, Bounds.Width / k, Bounds.Height / k).Intersect(new Rect(util.X, util.Y, util.Ancho, util.Alto));
        if (visible.Width > 0 && visible.Height > 0)
            using (ctx.PushClip(APantalla(util)))
            using (ctx.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation(ox, oy)))
                _lienzo.DibujarEnPapel(ctx, visible, lejos: k < 0.2);

        // los cortes entre hojas, de lado a lado del papel
        var corte = new Pen(new SolidColorBrush(Estilo.ConAlfa(Azul, 0xD8)), 1.4) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        for (int i = 1; i < c.Columnas; i++)
        {
            double x = c.CortesX[i] * k + ox;
            ctx.DrawLine(corte, new Point(x, papel.Top), new Point(x, papel.Bottom));
        }
        for (int i = 1; i < c.Filas; i++)
        {
            double y = c.CortesY[i] * k + oy;
            ctx.DrawLine(corte, new Point(papel.Left, y), new Point(papel.Right, y));
        }
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xD4)), 0.6), papel);

        // la marca de la versión gratuita, como saldrá: en el margen de arriba, a la derecha
        if (Marca)
        {
            var zona = APantalla(util);
            double tam = 2.6 / c.MmPorPx * k;
            if (tam >= 3)
            {
                var t = new FormattedText(T("Hecho con Ramo"), Cultura, FlowDirection.LeftToRight, LetraMarca, tam, GrisMarca);
                ctx.DrawText(t, new Point(zona.Right - t.Width, (papel.Top + zona.Top) / 2 - t.Height / 2));
            }
        }
    }
}
