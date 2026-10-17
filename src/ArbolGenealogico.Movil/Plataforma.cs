namespace ArbolGenealogico.Movil;

/// <summary>Lo que cada sistema hace a su manera.</summary>
public interface IPlataforma
{
    /// <summary>Carpeta privada de la aplicación donde se guardan los árboles (en Android, la copia de seguridad la incluye).</summary>
    string CarpetaDatos { get; }

    /// <summary>
    /// Abre el menú «Compartir» del sistema con esos archivos (WhatsApp, Telegram, correo, Drive, Bluetooth, Quick Share…).
    /// <paramref name="tipo"/> es el tipo MIME.
    /// </summary>
    Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo);

    /// <summary>Hace una foto con la cámara y devuelve sus bytes (JPEG), o null si se cancela o no hay cámara.</summary>
    Task<byte[]?> HacerFoto() => Task.FromResult<byte[]?>(null);

    bool HayCamara => false;
}

/// <summary>Para probar en el ordenador: los árboles van a una carpeta del usuario y «Compartir» la abre.</summary>
public sealed class PlataformaGenerica : IPlataforma
{
    public string CarpetaDatos { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArbolGenealogicoMovil");

    public Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo)
    {
        if (rutas.Count > 0 && OperatingSystem.IsWindows())
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{rutas[0]}\"");
        return Task.CompletedTask;
    }
}
