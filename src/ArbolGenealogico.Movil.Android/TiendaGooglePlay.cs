using Android.BillingClient.Api;
using Android.Content;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Android;

/// <summary>
/// La compra de «Ramo completo» con Google Play Billing: un producto de compra única (<see cref="Licencia.Producto"/>).
/// Sin servidor propio: si hay compra lo dice Google Play, con la cuenta del teléfono. Cada compra pagada se confirma
/// («acknowledge») enseguida, porque si no, Google la devuelve sola a los tres días.
/// </summary>
internal sealed class TiendaGooglePlay : Java.Lang.Object, ITienda, IPurchasesUpdatedListener
{
    private readonly BillingClient _cliente;
    private ProductDetails? _producto;
    private TaskCompletionSource<(ResultadoCompra, string?)>? _compra;

    public TiendaGooglePlay(Context contexto)
    {
        _cliente = BillingClient.NewBuilder(contexto)
            .SetListener(this)
            .EnablePendingPurchases(PendingPurchasesParams.NewBuilder().EnableOneTimeProducts().Build())
            .EnableAutoServiceReconnection()
            .Build();
    }

    /// <summary>La conexión que se está haciendo (si se pide el precio y se comprueba la compra a la vez, es una sola).</summary>
    private Task<bool>? _conexion;

    /// <summary>Se conecta con Google Play si no lo está. False si no se puede (sin Google Play, sin cuenta…).</summary>
    private Task<bool> Conectar()
    {
        if (_cliente.IsReady) return Task.FromResult(true);
        if (_conexion is { IsCompleted: false }) return _conexion;
        return _conexion = ConectarYa();
    }

    private async Task<bool> ConectarYa()
    {
        try
        {
            var r = await _cliente.StartConnectionAsync(() => { });
            return r.ResponseCode == BillingResponseCode.Ok;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Google Play Billing: " + ex.Message);
            return false;
        }
    }

    private async Task<ProductDetails?> Producto()
    {
        if (_producto != null) return _producto;
        if (!await Conectar()) return null;
        var parametros = QueryProductDetailsParams.NewBuilder()
            .SetProductList(new[]
            {
                QueryProductDetailsParams.Product.NewBuilder()
                    .SetProductId(Licencia.Producto)
                    .SetProductType(BillingClient.ProductType.Inapp)
                    .Build(),
            })
            .Build();
        var r = await _cliente.QueryProductDetailsAsync(parametros);
        if (r.Result.ResponseCode != BillingResponseCode.Ok) return null;
        return _producto = r.ProductDetails?.FirstOrDefault(p => p.ProductId == Licencia.Producto);
    }

    public async Task<string?> Precio()
    {
        try { return (await Producto())?.GetOneTimePurchaseOfferDetails()?.FormattedPrice; }
        catch { return null; }
    }

    public async Task<(ResultadoCompra Resultado, string? Motivo)> Comprar()
    {
        if (MainActivity.Actual is not { } actividad) return (ResultadoCompra.Fallida, null);
        var producto = await Producto();
        if (producto == null)
            return (ResultadoCompra.Fallida, _cliente.IsReady
                // conecta, pero el producto no está (aún no se ha creado o activado en Play Console)
                ? T("Ramo completo no está a la venta ahora mismo en Google Play. Vuelve a probar más tarde.")
                : T("No se ha podido conectar con Google Play. Comprueba la conexión a internet y vuelve a probar."));
        var parametros = BillingFlowParams.NewBuilder()
            .SetProductDetailsParamsList(new[]
            {
                BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(producto).Build(),
            })
            .Build();
        _compra = new TaskCompletionSource<(ResultadoCompra, string?)>();
        var inicio = _cliente.LaunchBillingFlow(actividad, parametros);
        if (inicio.ResponseCode != BillingResponseCode.Ok)
        {
            _compra = null;
            if (inicio.ResponseCode == BillingResponseCode.ItemAlreadyOwned)
                return await Comprado() == true ? (ResultadoCompra.YaComprada, null) : (ResultadoCompra.Fallida, null);
            return (ResultadoCompra.Fallida, null);
        }
        // el resultado llega a OnPurchasesUpdated cuando se cierra la ventana de pago
        return await _compra.Task;
    }

    public void OnPurchasesUpdated(BillingResult resultado, IList<Purchase>? compras)
    {
        var tcs = _compra;
        _compra = null;
        switch (resultado.ResponseCode)
        {
            case BillingResponseCode.Ok:
                _ = Atender(compras ?? Array.Empty<Purchase>(), tcs);
                break;
            case BillingResponseCode.UserCancelled:
                tcs?.TrySetResult((ResultadoCompra.Cancelada, null));
                break;
            case BillingResponseCode.ItemAlreadyOwned:
                _ = Task.Run(async () => tcs?.TrySetResult(await Comprado() == true ? (ResultadoCompra.YaComprada, null) : (ResultadoCompra.Fallida, null)));
                break;
            default:
                tcs?.TrySetResult((ResultadoCompra.Fallida, null));
                break;
        }
    }

    /// <summary>
    /// Lo que llega al comprar (o cuando se completa un pago pendiente, aunque no se esté comprando ahora): las pagadas se
    /// confirman y activan la versión completa; las pendientes, se esperan.
    /// </summary>
    private async Task Atender(IList<Purchase> compras, TaskCompletionSource<(ResultadoCompra, string?)>? tcs)
    {
        var resultado = (ResultadoCompra.Fallida, (string?)null);
        foreach (var c in compras.Where(EsRamoCompleto))
        {
            if (c.PurchaseState == PurchaseState.Purchased)
            {
                await Confirmar(c);
                Licencia.Activar();
                resultado = (ResultadoCompra.Hecha, null);
                break;
            }
            if (c.PurchaseState == PurchaseState.Pending) resultado = (ResultadoCompra.Pendiente, null);
        }
        tcs?.TrySetResult(resultado);
    }

    private static bool EsRamoCompleto(Purchase c) => c.Products.Contains(Licencia.Producto);

    private async Task Confirmar(Purchase c)
    {
        if (c.IsAcknowledged) return;
        try
        {
            var r = await _cliente.AcknowledgePurchaseAsync(AcknowledgePurchaseParams.NewBuilder().SetPurchaseToken(c.PurchaseToken).Build());
            if (r.ResponseCode != BillingResponseCode.Ok)
                System.Diagnostics.Debug.WriteLine("Confirmar la compra: " + r.DebugMessage);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Confirmar la compra: " + ex.Message); }
    }

    public async Task<bool?> Comprado()
    {
        if (!await Conectar()) return null;
        var r = await _cliente.QueryPurchasesAsync(QueryPurchasesParams.NewBuilder().SetProductType(BillingClient.ProductType.Inapp).Build());
        if (r.Result.ResponseCode != BillingResponseCode.Ok) return null;
        var pagada = r.Purchases?.FirstOrDefault(c => EsRamoCompleto(c) && c.PurchaseState == PurchaseState.Purchased);
        if (pagada == null) return false;
        // una compra pendiente que se completó con la aplicación cerrada: aún sin confirmar
        await Confirmar(pagada);
        return true;
    }
}
