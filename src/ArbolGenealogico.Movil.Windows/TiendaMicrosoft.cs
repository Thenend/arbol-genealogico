using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ArbolGenealogico.Movil;
using global::Windows.Services.Store;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Windows;

/// <summary>
/// La compra de «Ramo completo» en la Microsoft Store: un complemento duradero («Durable») de compra única cuyo id de
/// producto en Partner Center es <see cref="Licencia.Producto"/>. Sin servidor propio: si hay compra lo dice la tienda, con
/// la cuenta Microsoft de Windows. Solo funciona con la aplicación instalada desde su paquete (MSIX).
/// </summary>
internal sealed class TiendaMicrosoft : ITienda
{
    private StoreContext? _contexto;
    private StoreProduct? _producto;

    public string Nombre => "Microsoft Store";

    /// <summary>Si la aplicación se ejecuta desde su paquete (instalada de la tienda), no suelta (el .zip).</summary>
    public static bool Empaquetada()
    {
        try { return global::Windows.ApplicationModel.Package.Current != null; }
        catch { return false; }
    }

    /// <summary>
    /// La tienda, ligada a la ventana de Ramo: en una aplicación de escritorio, la ventana de pago necesita saber sobre qué
    /// ventana abrirse.
    /// </summary>
    private StoreContext Contexto()
    {
        if (_contexto != null) return _contexto;
        var contexto = StoreContext.GetDefault();
        if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.TryGetPlatformHandle()?.Handle is { } ventana
            && ventana != IntPtr.Zero)
            WinRT.Interop.InitializeWithWindow.Initialize(contexto, ventana);
        return _contexto = contexto;
    }

    private static bool EsRamoCompleto(string? idProducto) => idProducto == Licencia.Producto;

    private async Task<StoreProduct?> Producto()
    {
        if (_producto != null) return _producto;
        var r = await Contexto().GetAssociatedStoreProductsAsync(new[] { "Durable" });
        if (r.ExtendedError != null) throw r.ExtendedError;
        // el id que se le puso en Partner Center llega como InAppOfferToken (el StoreId lo inventa la tienda)
        return _producto = r.Products.Values.FirstOrDefault(p => EsRamoCompleto(p.InAppOfferToken));
    }

    public async Task<string?> Precio()
    {
        try { return (await Producto())?.Price.FormattedPrice; }
        catch { return null; }
    }

    public async Task<(ResultadoCompra Resultado, string? Motivo)> Comprar()
    {
        StoreProduct? producto;
        try { producto = await Producto(); }
        catch { return (ResultadoCompra.Fallida, T("No se ha podido conectar con {0}. Comprueba la conexión a internet y vuelve a probar.", Nombre)); }
        if (producto == null)
            // conecta, pero el complemento no está (aún no se ha creado o publicado en Partner Center)
            return (ResultadoCompra.Fallida, T("Ramo completo no está a la venta ahora mismo en {0}. Vuelve a probar más tarde.", Nombre));
        StorePurchaseResult r;
        try { r = await producto.RequestPurchaseAsync(); }
        catch { return (ResultadoCompra.Fallida, null); }
        switch (r.Status)
        {
            case StorePurchaseStatus.Succeeded:
                Licencia.Activar();
                return (ResultadoCompra.Hecha, null);
            case StorePurchaseStatus.AlreadyPurchased:
                Licencia.Activar();
                return (ResultadoCompra.YaComprada, null);
            case StorePurchaseStatus.NotPurchased:
                return (ResultadoCompra.Cancelada, null);
            default:
                return (ResultadoCompra.Fallida, null);
        }
    }

    public async Task<bool?> Comprado()
    {
        try
        {
            var licencia = await Contexto().GetAppLicenseAsync();
            if (licencia == null) return null;
            return licencia.AddOnLicenses.Values.Any(l => l.IsActive && EsRamoCompleto(l.InAppOfferToken));
        }
        catch { return null; }
    }
}
