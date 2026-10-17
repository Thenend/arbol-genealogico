using Android.Content;
using Android.Content.PM;
using AndroidX.Core.Content;
using JavaFile = Java.IO.File;

namespace ArbolGenealogico.Movil.Android;

/// <summary>Android: los árboles en la carpeta privada de la aplicación (entra en la copia de seguridad de Google) y el menú «Compartir» del sistema.</summary>
public sealed class PlataformaAndroid(Context contexto) : IPlataforma
{
    private string Autoridad => contexto.PackageName + ".archivos";

    public string CarpetaDatos => contexto.FilesDir!.AbsolutePath;

    public Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo)
    {
        var uris = rutas.Select(r => FileProvider.GetUriForFile(contexto, Autoridad, new JavaFile(r))!).ToList();
        Intent intent;
        if (uris.Count == 1)
        {
            intent = new Intent(Intent.ActionSend);
            intent.PutExtra(Intent.ExtraStream, uris[0]);
        }
        else
        {
            intent = new Intent(Intent.ActionSendMultiple);
            intent.PutParcelableArrayListExtra(Intent.ExtraStream, uris.Cast<global::Android.OS.IParcelable>().ToList());
        }
        intent.SetType(tipo);
        intent.PutExtra(Intent.ExtraTitle, titulo);
        // el nombre del archivo, para las aplicaciones que lo usan como asunto o título
        intent.PutExtra(Intent.ExtraSubject, Path.GetFileNameWithoutExtension(rutas[0]));
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        // para que las aplicaciones que reciben puedan leer todos (no solo el primero)
        intent.ClipData = ClipData.NewRawUri("", uris[0]);
        foreach (var u in uris.Skip(1)) intent.ClipData.AddItem(new ClipData.Item(u));
        var selector = Intent.CreateChooser(intent, titulo)!;
        selector.AddFlags(ActivityFlags.NewTask);
        (MainActivity.Actual ?? contexto).StartActivity(selector);
        return Task.CompletedTask;
    }

    public bool HayCamara => contexto.PackageManager?.HasSystemFeature(PackageManager.FeatureCameraAny) == true;

    public async Task<byte[]?> HacerFoto()
    {
        if (MainActivity.Actual is not { } actividad) return null;
        var carpeta = new JavaFile(contexto.CacheDir, "camara");
        carpeta.Mkdirs();
        var archivo = new JavaFile(carpeta, "foto.jpg");
        if (archivo.Exists()) archivo.Delete();
        var uri = FileProvider.GetUriForFile(contexto, Autoridad, archivo)!;
        if (!await actividad.HacerFoto(uri) || !archivo.Exists()) return null;
        return await File.ReadAllBytesAsync(archivo.AbsolutePath);
    }
}
