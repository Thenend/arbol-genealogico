using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ArbolGenealogico.Movil;

namespace ArbolGenealogico.Movil.Windows;

/// <summary>
/// Ramo en Windows. Al hacer doble clic en un archivo .ramo o .ged (el paquete los asocia a Ramo), llega su ruta y se
/// abre como un árbol recibido.
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        App.Plataforma = new PlataformaWindows();
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()
            .AfterSetup(_ => Dispatcher.UIThread.Post(() =>
            {
                Ajustar();
                foreach (var ruta in args.Where(File.Exists))
                {
                    try { App.Recibir(new ArchivoRecibido(Path.GetFileName(ruta), File.ReadAllBytes(ruta))); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("No se pudo leer " + ruta + ": " + ex.Message); }
                }
            }))
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// La ventana tiene la forma de un teléfono: en una pantalla de portátil no cabe con su alto, así que se deja un poco
    /// más baja que la pantalla, y en el centro.
    /// </summary>
    private static void Ajustar()
    {
        if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } ventana) return;
        ventana.MinWidth = 360;
        ventana.MinHeight = 480;
        if (ventana.Screens.ScreenFromWindow(ventana) is not { } pantalla) return;
        var zona = pantalla.WorkingArea;
        double k = pantalla.Scaling, alto = Math.Min(ventana.Height, zona.Height / k - 40), ancho = Math.Max(ventana.Width, 440);
        ventana.Width = ancho;
        ventana.Height = alto;
        ventana.Position = new PixelPoint(zona.X + (int)((zona.Width - ancho * k) / 2), zona.Y + (int)((zona.Height - alto * k) / 2));
    }
}
