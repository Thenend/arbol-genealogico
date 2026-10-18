using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// Encuadrar una foto en el círculo de la tarjeta: la foto se mueve con un dedo y se amplía con dos (o con la rueda del
/// ratón) bajo un círculo fijo. Devuelve el cuadrado elegido, en píxeles de la imagen, o null si se cancela.
/// </summary>
public static class EncuadrarFoto
{
    /// <summary>El encuadre abierto y su botón «Usar» (para las pruebas).</summary>
    internal static Encuadre? Actual { get; private set; }
    internal static Action? Usar { get; private set; }

    public static Task<SKRect?> Abrir(SKBitmap imagen, SKRect? inicial = null) =>
        Principal.Actual.Hoja<SKRect?>(cerrar => Crear(imagen, inicial ?? Fotos.Centrado(imagen), cerrar), null);

    private static Control Crear(SKBitmap imagen, SKRect inicial, Action<SKRect?> cerrar)
    {
        var raiz = new PanelConAtras();
        var encuadre = new Encuadre(imagen, inicial);
        Actual = encuadre;
        void Cerrar(SKRect? r) { Actual = null; Usar = null; cerrar(r); }
        Usar = () => Cerrar(encuadre.Cuadrado);
        raiz.AlPedirAtras = () => { Cerrar(null); return true; };

        var barra = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Background = Ui.Barra, MinHeight = 56 };
        barra.Children.Add(Ui.BotonIcono(Ui.Iconos.Cerrar, T("Cancelar"), () => Cerrar(null)));
        var titulo = Ui.Titulo(T("Encuadrar la foto"), 19);
        titulo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titulo, 1); barra.Children.Add(titulo);
        var usar = Ui.Boton(T("Usar"), () => Cerrar(encuadre.Cuadrado), principal: true);
        usar.Margin = new Thickness(0, 0, 10, 0); usar.Padding = new Thickness(18, 8);
        usar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(usar, 2); barra.Children.Add(usar);
        DockPanel.SetDock(barra, Dock.Top); raiz.Children.Add(barra);

        var ayuda = Ui.Parrafo(T("Mueve la foto con un dedo y amplíala con dos hasta que la cara quede dentro del círculo."), 15, Ui.Suave);
        ayuda.TextAlignment = TextAlignment.Center;
        ayuda.Margin = new Thickness(24, 12, 24, 24);
        DockPanel.SetDock(ayuda, Dock.Bottom); raiz.Children.Add(ayuda);

        raiz.Children.Add(encuadre);
        return raiz;
    }

    /// <summary>La foto bajo el círculo. El estado es el cuadrado de la imagen que cae dentro del círculo.</summary>
    internal sealed class Encuadre : Control
    {
        private readonly Bitmap _bmp;
        private readonly double _w, _h;
        private double _cx, _cy, _lado;                 // el cuadrado elegido: centro y lado, en píxeles de la imagen
        private readonly Dictionary<long, Point> _dedos = new();
        private double _pinchLado, _pinchDist, _pinchIx, _pinchIy;

        public Encuadre(SKBitmap imagen, SKRect inicial)
        {
            _w = imagen.Width; _h = imagen.Height;
            using (var datos = imagen.Encode(SKEncodedImageFormat.Jpeg, 92))
            using (var ms = new MemoryStream(datos.ToArray()))
                _bmp = new Bitmap(ms);
            _cx = inicial.MidX; _cy = inicial.MidY; _lado = inicial.Width;
            Limitar();
            ClipToBounds = true;
            Focusable = true;
        }

        public SKRect Cuadrado => SKRect.Create((float)(_cx - _lado / 2), (float)(_cy - _lado / 2), (float)_lado, (float)_lado);

        /// <summary>Lo más que se puede ampliar: que el círculo abarque al menos un décimo del lado corto de la foto.</summary>
        private double LadoMinimo => Math.Max(16, Math.Min(_w, _h) / 10);

        private void Limitar()
        {
            // el círculo siempre dentro de la foto: ni más grande que ella ni saliéndose por un lado
            _lado = Math.Clamp(_lado, Math.Min(LadoMinimo, Math.Min(_w, _h)), Math.Min(_w, _h));
            _cx = Math.Clamp(_cx, _lado / 2, _w - _lado / 2);
            _cy = Math.Clamp(_cy, _lado / 2, _h - _lado / 2);
        }

        /// <summary>El círculo en la pantalla: centrado, con un margen alrededor.</summary>
        private (Point Centro, double Diametro) Circulo()
        {
            double d = Math.Max(40, Math.Min(Bounds.Width, Bounds.Height) - 48);
            return (new Point(Bounds.Width / 2, Bounds.Height / 2), d);
        }

        /// <summary>Píxeles de pantalla por píxel de la imagen.</summary>
        private double Escala => Circulo().Diametro / _lado;

        public override void Render(DrawingContext ctx)
        {
            ctx.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            var (c, d) = Circulo();
            double k = d / _lado;
            var destino = new Rect(c.X - _cx * k, c.Y - _cy * k, _w * k, _h * k);
            ctx.DrawImage(_bmp, new Rect(0, 0, _bmp.Size.Width, _bmp.Size.Height), destino);

            // fuera del círculo, la foto se oscurece: así se ve lo que quedará en la tarjeta
            var circulo = new Rect(c.X - d / 2, c.Y - d / 2, d, d);
            var velo = new GeometryGroup { FillRule = FillRule.EvenOdd };
            velo.Children.Add(new RectangleGeometry(new Rect(Bounds.Size)));
            velo.Children.Add(new EllipseGeometry(circulo));
            ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(0xB0, 0, 0, 0)), null, velo);
            ctx.DrawEllipse(null, new Pen(Brushes.White, 2), circulo.Center, d / 2, d / 2);
        }

        /// <summary>Desplaza la foto como al arrastrarla <paramref name="dx"/>, <paramref name="dy"/> píxeles de pantalla.</summary>
        public void Mover(double dx, double dy)
        {
            double k = Escala;
            _cx -= dx / k; _cy -= dy / k;
            Limitar();
            InvalidateVisual();
        }

        /// <summary>Amplía (factor &gt; 1) o reduce la foto manteniendo quieto el punto de la pantalla <paramref name="p"/>.</summary>
        public void Ampliar(double factor, Point? p = null)
        {
            var (c, _) = Circulo();
            var q = p ?? c;
            double k = Escala;
            double ix = _cx + (q.X - c.X) / k, iy = _cy + (q.Y - c.Y) / k;
            _lado /= factor;
            Limitar();
            double k2 = Escala;
            _cx = ix - (q.X - c.X) / k2; _cy = iy - (q.Y - c.Y) / k2;
            Limitar();
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _dedos[e.Pointer.Id] = e.GetPosition(this);
            e.Pointer.Capture(this);
            if (_dedos.Count == 2) EmpezarPinch();
            e.Handled = true;
        }

        private void EmpezarPinch()
        {
            var ps = _dedos.Values.Take(2).ToArray();
            var (c, _) = Circulo();
            var centro = new Point((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
            double k = Escala;
            _pinchDist = Math.Max(1, Distancia(ps[0], ps[1]));
            _pinchLado = _lado;
            // el punto de la foto que está entre los dos dedos se queda entre ellos
            _pinchIx = _cx + (centro.X - c.X) / k; _pinchIy = _cy + (centro.Y - c.Y) / k;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_dedos.TryGetValue(e.Pointer.Id, out var previo)) return;
            var p = e.GetPosition(this);
            _dedos[e.Pointer.Id] = p;
            if (_dedos.Count >= 2)
            {
                var ps = _dedos.Values.Take(2).ToArray();
                var (c, _) = Circulo();
                var centro = new Point((ps[0].X + ps[1].X) / 2, (ps[0].Y + ps[1].Y) / 2);
                _lado = _pinchLado * _pinchDist / Math.Max(1, Distancia(ps[0], ps[1]));
                Limitar();
                double k = Escala;
                _cx = _pinchIx - (centro.X - c.X) / k; _cy = _pinchIy - (centro.Y - c.Y) / k;
                Limitar();
                InvalidateVisual();
                return;
            }
            Mover(p.X - previo.X, p.Y - previo.Y);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (!_dedos.Remove(e.Pointer.Id)) return;
            if (_dedos.Count == 0) e.Pointer.Capture(null);
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _dedos.Remove(e.Pointer.Id);
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            Ampliar(Math.Pow(1.15, e.Delta.Y), e.GetPosition(this));
            e.Handled = true;
        }

        private static double Distancia(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
