using ArbolGenealogico.Core.Model;
using Avalonia.Threading;

namespace ArbolGenealogico.Movil;

/// <summary>
/// La versión completa («Ramo completo»): una compra única en la tienda, sin suscripción ni servidor propio. Sin ella se
/// puede ver y editar todo, pero se tiene <see cref="ArbolesGratis"/> árbol propio (los recibidos y el de ejemplo no
/// cuentan), cada árbol crece hasta <see cref="PersonasGratis"/> personas, no se enlazan árboles y lo que se exporta (PDF,
/// imagen, hojas para imprimir) lleva la marca «Hecho con Ramo».
/// </summary>
public static class Licencia
{
    /// <summary>El id del producto en Google Play Console (un producto de compra única, no una suscripción).</summary>
    public const string Producto = "ramo_completo";

    /// <summary>Los árboles propios (creados o importados de GEDCOM) que se pueden tener sin la versión completa.</summary>
    public const int ArbolesGratis = 1;

    /// <summary>Hasta cuántas personas puede crecer un árbol sin la versión completa (editar las que ya tiene, siempre).</summary>
    public const int PersonasGratis = 50;

    /// <summary>Si se ha comprado (o si no hay tienda: entonces no hay nada que comprar).</summary>
    public static bool Completa => App.Plataforma.Tienda == null || Preferencias.Actual.Completa;

    /// <summary>Lo que se exporta lleva la marca «Hecho con Ramo».</summary>
    public static bool ConMarca => !Completa;

    /// <summary>Al comprarse (o al deshacerse la compra, si se devuelve).</summary>
    public static event Action? Cambiada;

    public static bool PuedeCrearArbol => Completa || (ContarPropios?.Invoke() ?? App.Biblioteca.ArbolesPropios()) < ArbolesGratis;

    /// <summary>
    /// En el ordenador los árboles están donde se guarden, no en la biblioteca: cuántos propios hay lo cuenta él (ver
    /// <see cref="Preferencias.PropiosPc"/>).
    /// </summary>
    public static Func<int>? ContarPropios { get; set; }

    /// <summary>Si a ese árbol se le pueden añadir personas: mientras tenga menos de <see cref="PersonasGratis"/>.</summary>
    public static bool PuedeAnadirPersonas(Arbol a) => Completa || a.Personas.Count < PersonasGratis;

    /// <summary>Si una fusión que deja el árbol con <paramref name="total"/> personas (<paramref name="nuevas"/> más) se puede hacer.</summary>
    public static bool PuedeFusionar(int total, int nuevas) => Completa || nuevas == 0 || total <= PersonasGratis;

    /// <summary>La tienda confirma la compra (al comprar, al restaurar, al arrancar o cuando se completa un pago pendiente).</summary>
    public static void Activar() => Poner(true);

    private static void Poner(bool completa)
    {
        void Hacer()
        {
            var p = Preferencias.Actual;
            if (p.Completa == completa) return;
            p.Completa = completa;
            p.Guardar();
            Cambiada?.Invoke();
        }
        // la tienda puede avisar desde otro hilo
        if (Dispatcher.UIThread.CheckAccess()) Hacer(); else Dispatcher.UIThread.Post(Hacer);
    }

    /// <summary>
    /// Al abrir la aplicación, se pregunta a la tienda (sin servidor: lo sabe la propia tienda, con la cuenta del teléfono).
    /// Lo recordado solo se quita si la tienda contesta que no hay compra (p. ej. se devolvió); sin conexión se queda igual.
    /// </summary>
    public static async Task Comprobar()
    {
        if (App.Plataforma.Tienda is not { } tienda) return;
        try
        {
            if (await tienda.Comprado() is bool comprado) Poner(comprado);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Comprobar la compra: " + ex.Message); }
    }

    /// <summary>«Restaurar compra»: true si la hay, false si la tienda dice que no, null si no se ha podido preguntar.</summary>
    public static async Task<bool?> Restaurar()
    {
        if (App.Plataforma.Tienda is not { } tienda) return true;
        bool? r;
        try { r = await tienda.Comprado(); }
        catch { r = null; }
        if (r == true) Poner(true);
        return r;
    }
}

/// <summary>Cómo acaba una compra.</summary>
public enum ResultadoCompra
{
    /// <summary>Pagada y confirmada: ya es la versión completa.</summary>
    Hecha,
    /// <summary>Pagada con un medio que tarda (efectivo en una tienda, transferencia…): se activa cuando se complete.</summary>
    Pendiente,
    /// <summary>La persona ha cerrado la ventana de pago.</summary>
    Cancelada,
    /// <summary>Ya estaba comprada con esta cuenta (y ya se ha activado).</summary>
    YaComprada,
    /// <summary>No se ha podido (sin conexión, la tienda no está disponible…).</summary>
    Fallida,
}

/// <summary>La tienda de la aplicación (Google Play en Android, Microsoft Store en Windows): el precio, comprar y saber si ya se compró.</summary>
public interface ITienda
{
    /// <summary>Cómo se llama la tienda en los avisos («Google Play», «Microsoft Store»).</summary>
    string Nombre => "Google Play";

    /// <summary>El precio como lo da la tienda, con su moneda («4,99 €»), o null si no se ha podido saber.</summary>
    Task<string?> Precio();

    /// <summary>Abre la ventana de pago de la tienda. Con <see cref="ResultadoCompra.Fallida"/>, el motivo (si se sabe).</summary>
    Task<(ResultadoCompra Resultado, string? Motivo)> Comprar();

    /// <summary>
    /// Si esta cuenta tiene la compra (y, si aún no estaba confirmada a la tienda, la confirma). Null si no se ha podido
    /// preguntar (sin conexión…).
    /// </summary>
    Task<bool?> Comprado();
}

/// <summary>
/// Una tienda de mentira para probar en el ordenador: ARBOL_COMPLETA=1 empieza con la versión completa y ARBOL_COMPRA dice
/// cómo acaba la compra («pendiente», «cancelada», «fallida»; si no, se hace). Con <paramref name="recordar"/> (la versión de
/// prueba de Android), la compra se queda al volver a abrir la aplicación.
/// </summary>
public sealed class TiendaPrueba(bool recordar = false) : ITienda
{
    private bool _comprada = Environment.GetEnvironmentVariable("ARBOL_COMPLETA") == "1";

    public async Task<string?> Precio()
    {
        await Task.Delay(300);
        return "4,99 €";
    }

    public async Task<(ResultadoCompra, string?)> Comprar()
    {
        await Task.Delay(500);
        switch (Environment.GetEnvironmentVariable("ARBOL_COMPRA"))
        {
            case "pendiente": return (ResultadoCompra.Pendiente, null);
            case "cancelada": return (ResultadoCompra.Cancelada, null);
            case "fallida": return (ResultadoCompra.Fallida, "Google Play no está disponible");
        }
        _comprada = true;
        Licencia.Activar();
        return (ResultadoCompra.Hecha, null);
    }

    public Task<bool?> Comprado() => Task.FromResult<bool?>(_comprada || (recordar && Preferencias.Actual.Completa));
}
