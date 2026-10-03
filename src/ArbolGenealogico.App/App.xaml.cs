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
            try { Renderizar(args[1], args[2], args.Length > 3 ? double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 1.0, args.Length > 4 ? args[4] : null); }
            catch (Exception ex) { File.WriteAllText(args[2] + ".error.txt", ex.ToString()); codigo = 2; }
            Shutdown(codigo);
            return;
        }
        var ventana = new MainWindow(args.FirstOrDefault(a => a.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(a)));
        MainWindow = ventana;
        ventana.Show();
    }

    private static void Renderizar(string entrada, string salida, double escala, string? seleccion)
    {
        var arbol = ArbolJson.Cargar(entrada, out _);
        var vista = new VistaArbol();
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
