using Microsoft.JSInterop;

namespace ArbolGenealogico.Web.Servicios;

public sealed record Usuario(string Id, string Email);
public sealed record ResumenArbol(string Id, string Nombre, string Rol, int Personas, string? Actualizado, string ActualizadoPor, string PropietarioEmail);
public sealed record ArbolRemoto(string Id, string Nombre, string Datos, int Version, string Rol);
public sealed record Miembro(string Usuario, string Email, string Rol, bool Pendiente);
public sealed record ArbolDirectorio(string Id, string Nombre, int Personas, string? Rol, bool Solicitado);
public sealed record PersonaDirectorio(string Usuario, string Email, string Nombre, string? Visto, List<ArbolDirectorio> Arboles)
{
    public string Mostrar => string.IsNullOrWhiteSpace(Nombre) ? Email : Nombre;
}
public sealed record Solicitud(string ArbolId, string Arbol, string Usuario, string Email, string Nombre, string Mensaje, string? Creada)
{
    public string Quien => string.IsNullOrWhiteSpace(Nombre) ? Email : $"{Nombre} ({Email})";
}

/// <summary>Configuración de Supabase (en wwwroot/appsettings.json). Vacía: modo de prueba, todo en este navegador.</summary>
public sealed class ConfigSupabase
{
    public string Url { get; set; } = "";
    public string Clave { get; set; } = "";
}

/// <summary>Cuentas y árboles guardados en Supabase (o en el navegador, en modo de prueba). Ver wwwroot/js/datos.js.</summary>
public sealed class Datos(IJSRuntime js, ConfigSupabase config) : IAsyncDisposable
{
    private IJSObjectReference? _m;
    private DotNetObjectReference<Datos>? _ref;
    private Task? _inicio;

    public Usuario? Actual { get; private set; }
    public bool Local { get; private set; }
    public bool Listo { get; private set; }
    /// <summary>Se ha entrado con el enlace de «He olvidado mi contraseña»: hay que pedir la nueva.</summary>
    public bool RecuperandoClave { get; set; }
    public event Action? Cambio;
    public void Avisar() => Cambio?.Invoke();

    public Task Iniciar() => _inicio ??= IniciarAsync();

    private async Task IniciarAsync()
    {
        _m = await js.InvokeAsync<IJSObjectReference>("import", "./js/datos.js");
        var modo = await _m.InvokeAsync<string>("iniciar", config.Url, config.Clave);
        Local = modo == "local";
        Actual = await _m.InvokeAsync<Usuario?>("usuario");
        _ref = DotNetObjectReference.Create(this);
        await _m.InvokeVoidAsync("alCambiarSesion", _ref);
        if (Actual != null) await AceptarInvitaciones();
        Listo = true;
        Cambio?.Invoke();
    }

    [JSInvokable]
    public async Task SesionCambiada(string evento, string? id, string? email)
    {
        var antes = Actual?.Id;
        Actual = id == null ? null : new Usuario(id, email ?? "");
        if (evento == "PASSWORD_RECOVERY") RecuperandoClave = true;
        if (Actual != null && antes != Actual.Id) await AceptarInvitaciones();
        Cambio?.Invoke();
    }

    private async Task AceptarInvitaciones()
    {
        try
        {
            await M.InvokeAsync<int>("aceptarInvitaciones");
            await M.InvokeVoidAsync("entrarPerfil", (string?)null);
            MiNombre = await M.InvokeAsync<string>("miNombre");
        }
        catch { /* sin conexión (o base de datos sin preparar): se reintenta al volver a entrar */ }
    }

    /// <summary>Mi nombre en el directorio de la familia ("" si aún no lo he puesto).</summary>
    public string MiNombre { get; private set; } = "";

    public async Task PonerMiNombre(string nombre)
    {
        await M.InvokeVoidAsync("entrarPerfil", nombre.Trim());
        MiNombre = nombre.Trim();
        Cambio?.Invoke();
    }

    public ValueTask<List<PersonaDirectorio>> Directorio() => M.InvokeAsync<List<PersonaDirectorio>>("directorio");
    public ValueTask<string?> PedirAcceso(string arbol, string mensaje) => M.InvokeAsync<string?>("pedirAcceso", arbol, mensaje);
    public ValueTask<string?> CancelarSolicitud(string arbol) => M.InvokeAsync<string?>("cancelarSolicitud", arbol);
    public ValueTask<List<Solicitud>> MisSolicitudes() => M.InvokeAsync<List<Solicitud>>("misSolicitudes");
    public ValueTask<string?> RechazarSolicitud(string arbol, string usuario) => M.InvokeAsync<string?>("rechazarSolicitud", arbol, usuario);
    public ValueTask<string?> CompartirCon(string arbol, string usuario, string rol) => M.InvokeAsync<string?>("compartirCon", arbol, usuario, rol);
    public ValueTask<bool> EsVisible(string arbol) => M.InvokeAsync<bool>("esVisible", arbol);
    public ValueTask<string?> PonerVisible(string arbol, bool visible) => M.InvokeAsync<string?>("ponerVisible", arbol, visible);

    private IJSObjectReference M => _m ?? throw new InvalidOperationException("Datos sin iniciar");

    public ValueTask<string?> Entrar(string email, string clave) => M.InvokeAsync<string?>("entrar", email, clave);
    public ValueTask<string?> Registrar(string email, string clave) => M.InvokeAsync<string?>("registrar", email, clave);
    public ValueTask<string?> EnlaceMagico(string email) => M.InvokeAsync<string?>("enlaceMagico", email);
    public ValueTask<string?> Recuperar(string email) => M.InvokeAsync<string?>("recuperar", email);
    public ValueTask<string?> CambiarClave(string clave) => M.InvokeAsync<string?>("cambiarClave", clave);
    public async Task Salir() { await M.InvokeVoidAsync("salir"); Actual = null; Cambio?.Invoke(); }

    public ValueTask<List<ResumenArbol>> Listar() => M.InvokeAsync<List<ResumenArbol>>("listar");
    public ValueTask<ArbolRemoto?> Cargar(string id) => M.InvokeAsync<ArbolRemoto?>("cargar", id);
    public ValueTask<int> Version(string id) => M.InvokeAsync<int>("version", id);
    public ValueTask<string> Crear(string nombre, string datos) => M.InvokeAsync<string>("crear", nombre, datos);
    public ValueTask<int> Guardar(string id, string nombre, string datos, int? version) => M.InvokeAsync<int>("guardar", id, nombre, datos, version);
    public ValueTask Borrar(string id) => M.InvokeVoidAsync("borrar", id);
    public ValueTask SalirDeArbol(string id) => M.InvokeVoidAsync("salirDeArbol", id);

    public ValueTask<List<Miembro>> Miembros(string id) => M.InvokeAsync<List<Miembro>>("miembros", id);
    public ValueTask<string?> Invitar(string id, string email, string rol) => M.InvokeAsync<string?>("invitar", id, email, rol);
    public ValueTask<string?> CambiarRol(string id, Miembro m, string rol) => M.InvokeAsync<string?>("cambiarRol", id, m.Usuario, m.Email, rol, m.Pendiente);
    public ValueTask<string?> Quitar(string id, Miembro m) => M.InvokeAsync<string?>("quitar", id, m.Usuario, m.Email, m.Pendiente);
    public ValueTask<int> CopiarMiembros(string origen, string destino) => M.InvokeAsync<int>("copiarMiembros", origen, destino);

    public ValueTask Suscribir<T>(string id, DotNetObjectReference<T> receptor) where T : class => M.InvokeVoidAsync("suscribir", id, receptor);
    public ValueTask Desuscribir() => M.InvokeVoidAsync("desuscribir");

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        if (_m != null) await _m.DisposeAsync();
    }
}
