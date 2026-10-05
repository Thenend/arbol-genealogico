using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArbolGenealogico.Core.Io;

namespace ArbolGenealogico.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args;
        if (args.Length >= 3 && args[0] == "--render")
        {
            // Modo de pruebas: dibuja un árbol JSON en un PNG sin abrir ventanas.
            int codigo = 0;
            try
            {
                Renderizar(args[1], args[2], args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 1.0,
                    args.Length > 4 && args[4] != "-" ? args[4] : null,
                    args.Length > 5 && Enum.TryParse<Core.Layout.Ordenacion>(args[5], true, out var ord) ? ord : Core.Layout.Ordenacion.C,
                    FormaTarjeta.Leer(args.Length > 6 ? args[6] : ""));
            }
            catch (Exception ex) { File.WriteAllText(args[2] + ".error.txt", ex.ToString()); codigo = 2; }
            Shutdown(codigo);
            return;
        }
        if (args.Length >= 3 && args[0] == "--imprimir-png")
        {
            // Modo de pruebas: guarda como PNG las hojas que se imprimirían (papel A4..A1, de 1 a 8 hojas).
            int codigo = 0;
            try
            {
                var arbol = ArbolJson.Cargar(args[1], out _);
                var orden = args.Length > 5 && Enum.TryParse<Core.Layout.Ordenacion>(args[5], true, out var o) ? o : Core.Layout.Ordenacion.C;
                var tarjetas = FormaTarjeta.Leer(args.Length > 6 ? args[6] : "");
                var vista = new VistaArbol { Ordenacion = orden, Tarjetas = tarjetas };
                vista.Cargar(arbol, false);
                var c = GuiaImpresion.Mejor(vista.Layout, args.Length > 3 ? args[3] : "A4", args.Length > 4 ? int.Parse(args[4]) : 1,
                    !args.Any(x => x.Equals("iguales", StringComparison.OrdinalIgnoreCase)))!;
                var estilo = args.Any(x => x.Equals("oscura", StringComparison.OrdinalIgnoreCase)) ? EstiloPapel.Oscuro
                           : args.Any(x => x.Equals("bn", StringComparison.OrdinalIgnoreCase)) ? EstiloPapel.BlancoYNegro : EstiloPapel.Claro;
                var dpi = args.Select(x => x.StartsWith("dpi=") && double.TryParse(x[4..], out var d) ? d : 0).FirstOrDefault(d => d > 0);
                Impresion.GuardarPng(arbol, orden, tarjetas, estilo, c, args[2], dpi > 0 ? dpi : 60);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(args[2], "error.txt"), ex.ToString()); codigo = 2; }
            Shutdown(codigo);
            return;
        }
        var ventana = new MainWindow(args.FirstOrDefault(a => a.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(a)));
        MainWindow = ventana;
        ventana.Show();
    }

    private static void Renderizar(string entrada, string salida, double escala, string? seleccion, Core.Layout.Ordenacion ordenacion, FormaTarjeta tarjetas)
    {
        var arbol = ArbolJson.Cargar(entrada, out _);
        var vista = new VistaArbol { Ordenacion = ordenacion, Tarjetas = tarjetas };
        vista.Cargar(arbol, false);
        if (seleccion != null) vista.SeleccionId = seleccion;
        vista.RestablecerCamara();
        double w = Math.Ceiling(vista.Layout!.Ancho), h = Math.Ceiling(vista.Layout.Alto);
        vista.Width = w; vista.Height = h;
        vista.Measure(new Size(w, h));
        vista.Arrange(new Rect(0, 0, w, h));
        vista.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)(w * escala), (int)(h * escala), 96 * escala, 96 * escala, PixelFormats.Pbgra32);
        rtb.Render(vista);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(salida);
        enc.Save(fs);
    }
}
