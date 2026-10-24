using Avalonia;
using Avalonia.Threading;
using ArbolGenealogico.Movil;

namespace ArbolGenealogico.Movil.Windows;

/// <summary>
/// Ramo en Windows: la ventana del ordenador (barra de herramientas, atajos, minimapa) con el motor del móvil. Al hacer
/// doble clic en un archivo .ramo o .ged (el paquete los asocia a Ramo), llega su ruta y se
/// abre como un árbol recibido.
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        App.Plataforma = new PlataformaWindows();
        App.Escritorio = true;
        return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()
            .AfterSetup(_ => Dispatcher.UIThread.Post(() =>
            {
                foreach (var ruta in args.Where(File.Exists))
                {
                    try { App.Recibir(new ArchivoRecibido(Path.GetFileName(ruta), File.ReadAllBytes(ruta), Path.GetFullPath(ruta))); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("No se pudo leer " + ruta + ": " + ex.Message); }
                }
            }))
            .StartWithClassicDesktopLifetime(args);
    }
}
