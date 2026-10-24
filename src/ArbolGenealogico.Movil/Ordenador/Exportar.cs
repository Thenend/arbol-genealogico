using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ArbolGenealogico.Movil.Vistas;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Ordenador;

/// <summary>
/// En el ordenador, lo que en el móvil se comparte se guarda: se pregunta dónde (junto al árbol, con su nombre) y luego
/// se ofrece abrirlo o enseñarlo en su carpeta.
/// </summary>
public static class Exportar
{
    private static IStorageProvider? Almacen => TopLevel.GetTopLevel(Principal.Actual)?.StorageProvider;

    /// <summary>Pregunta dónde guardar un archivo exportado. Null si se cancela.</summary>
    public static async Task<string?> Donde(string nombre, string extension, string tipo, string? junto = null)
    {
        if (Almacen is not { } almacen) return null;
        var carpeta = junto is { } r && Path.GetDirectoryName(r) is { Length: > 0 } dir ? await almacen.TryGetFolderFromPathAsync(dir) : null;
        var archivo = await almacen.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = T("Exportar"), SuggestedFileName = Biblioteca.Limpiar(nombre) + extension, DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices = new[] { new FilePickerFileType(tipo) { Patterns = new[] { "*" + extension } } }, ShowOverwritePrompt = true,
            SuggestedStartLocation = carpeta ?? await almacen.TryGetWellKnownFolderAsync(WellKnownFolder.Documents),
        });
        return archivo?.TryGetLocalPath();
    }

    /// <summary>Guarda donde se elija una copia del archivo ya hecho (el PDF de las hojas o de la lámina).</summary>
    public static async Task GuardarCopia(string hecho, string nombre, string tipo, string? junto = null)
    {
        if (await Donde(nombre, Path.GetExtension(hecho), tipo, junto) is not { } ruta) return;
        File.Copy(hecho, ruta, true);
        await Hecho(ruta);
    }

    /// <summary>Ya está guardado: abrirlo, enseñarlo en su carpeta o nada.</summary>
    public static async Task Hecho(string ruta)
    {
        int r = await Principal.Actual.Preguntar(T("Guardado"), ruta, T("Abrirlo"), T("Mostrar en la carpeta"), T("Cerrar"));
        try
        {
            if (r == 0) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ruta) { UseShellExecute = true });
            else if (r == 1 && OperatingSystem.IsWindows()) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ruta}\"");
            else if (r == 1) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(ruta)!) { UseShellExecute = true });
        }
        catch (Exception ex) { await Principal.Actual.Error(T("No se pudo completar"), ex); }
    }
}
