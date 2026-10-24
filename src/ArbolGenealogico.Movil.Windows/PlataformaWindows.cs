using System.Diagnostics;
using ArbolGenealogico.Movil;

namespace ArbolGenealogico.Movil.Windows;

/// <summary>
/// Windows: los árboles en la carpeta de la aplicación, «Compartir» enseña el archivo en el Explorador e «Imprimir» abre el
/// PDF con el programa de PDF del sistema (el navegador, normalmente), que tiene su botón de imprimir.
/// </summary>
internal sealed class PlataformaWindows : IPlataforma
{
    /// <summary>Instalada desde la Microsoft Store se compra ahí; sin paquete (el .zip) no hay tienda y todo está incluido.</summary>
    public ITienda? Tienda { get; } = TiendaMicrosoft.Empaquetada() ? new TiendaMicrosoft() : null;

    public string CarpetaDatos { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ramo");

    public Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo)
    {
        if (rutas.Count > 0) Process.Start("explorer.exe", $"/select,\"{rutas[0]}\"");
        return Task.CompletedTask;
    }

    public Task Imprimir(string rutaPdf, string titulo, Core.Presentacion.Papel papel, bool vertical, int hojas)
    {
        Process.Start(new ProcessStartInfo(rutaPdf) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
