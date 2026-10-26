namespace ArbolGenealogico.Movil;

/// <summary>Lo que cada sistema hace a su manera.</summary>
public interface IPlataforma
{
    /// <summary>Carpeta privada de la aplicación donde se guardan los árboles (en Android, la copia de seguridad la incluye).</summary>
    string CarpetaDatos { get; }

    /// <summary>
    /// Abre el menú «Compartir» del sistema con esos archivos (WhatsApp, Telegram, correo, Drive, Bluetooth, Quick Share…).
    /// <paramref name="tipo"/> es el tipo MIME. <paramref name="texto"/>, si lo hay, va como mensaje junto a los archivos
    /// (el pie de foto en WhatsApp, el cuerpo del correo) y la persona puede cambiarlo o borrarlo antes de mandarlo.
    /// </summary>
    Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo, string? texto = null);

    /// <summary>
    /// Manda a imprimir un PDF con el sistema de impresión (con el papel y la orientación de sus <paramref name="hojas"/>
    /// páginas ya elegidos). Donde no hay, se comparte.
    /// </summary>
    Task Imprimir(string rutaPdf, string titulo, Core.Presentacion.Papel papel, bool vertical, int hojas) =>
        Compartir(new[] { rutaPdf }, "application/pdf", titulo);

    /// <summary>Hace una foto con la cámara y devuelve sus bytes (JPEG), o null si se cancela o no hay cámara.</summary>
    Task<byte[]?> HacerFoto() => Task.FromResult<byte[]?>(null);

    bool HayCamara => false;

    /// <summary>
    /// Dónde se guarda la copia de seguridad de los árboles fuera de la carpeta privada (que se pierde al desinstalar la
    /// aplicación), tal como la ve el usuario («Documentos/Ramo»); null si en este sistema no hay.
    /// </summary>
    string? CarpetaCopia => null;

    /// <summary>El tamaño de letra elegido en el sistema (1 = normal).</summary>
    double EscalaLetra => 1;

    /// <summary>La cultura del idioma en el que se quiere la aplicación («es-ES», «pt-BR»…).</summary>
    string Idioma => System.Globalization.CultureInfo.CurrentUICulture.Name;

    /// <summary>Escribe (o reescribe) la copia de un árbol con ese nombre de archivo.</summary>
    Task GuardarCopia(string nombreArchivo, byte[] datos) => Task.CompletedTask;

    /// <summary>Quita la copia de ese nombre (al cambiar el nombre del árbol), si la escribió esta instalación.</summary>
    Task BorrarCopia(string nombreArchivo) => Task.CompletedTask;

    /// <summary>
    /// Lee todos los árboles de la carpeta de la copia, pidiendo permiso para esa carpeta (tras reinstalar, la
    /// aplicación ya no puede leer sola lo que escribió antes). Null si se cancela o el sistema no lo permite.
    /// </summary>
    Task<IReadOnlyList<ArchivoRecibido>?> LeerCopias() => Task.FromResult<IReadOnlyList<ArchivoRecibido>?>(null);

    bool PuedeLeerCopias => false;

    /// <summary>La tienda donde se compra la versión completa; null si no hay (entonces todo está incluido).</summary>
    ITienda? Tienda => null;

    // ---------- Avisos de cumpleaños ----------

    /// <summary>Si el sistema puede avisar de los cumpleaños aunque la aplicación esté cerrada (Android).</summary>
    bool HayAvisos => false;

    /// <summary>Si el sistema deja que la aplicación muestre avisos (desde Android 13 hay que pedir permiso).</summary>
    bool AvisosPermitidos => false;

    /// <summary>Pide permiso para mostrar avisos, si hace falta. Devuelve si los deja.</summary>
    Task<bool> PedirPermisoAvisos() => Task.FromResult(AvisosPermitidos);

    /// <summary>Abre los ajustes del sistema de los avisos de la aplicación (si se bloquearon allí).</summary>
    void AbrirAjustesAvisos() { }

    /// <summary>Programa el aviso de cada día a esa hora (null: sin avisos).</summary>
    void ProgramarAvisos(int? hora) { }
}

/// <summary>Para probar en el ordenador: los árboles van a una carpeta del usuario y «Compartir» la abre.</summary>
public sealed class PlataformaGenerica : IPlataforma
{
    public ITienda? Tienda { get; } = new TiendaPrueba();

    public string CarpetaDatos { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArbolGenealogicoMovil");

    public Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo, string? texto = null)
    {
        if (rutas.Count > 0 && OperatingSystem.IsWindows())
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{rutas[0]}\"");
        return Task.CompletedTask;
    }
}
