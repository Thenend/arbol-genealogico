using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Avalonia.Android;
using Uri = Android.Net.Uri;

namespace ArbolGenealogico.Movil.Android;

/// <summary>
/// La única pantalla de Android. Además de abrirse desde el icono, aparece en «Abrir con» y en «Compartir» de otras
/// aplicaciones (WhatsApp, Telegram, Gmail, el gestor de archivos…) para los árboles (.tree y los antiguos .json).
/// </summary>
[Activity(
    Label = "Ramo", Theme = "@style/Tema.Arbol", Icon = "@mipmap/icono", MainLauncher = true, Exported = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
// «Abrir con»: las aplicaciones de mensajería mandan los .tree como application/octet-stream (no conocen la extensión)
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" }, DataMimeTypes = new[] { "application/octet-stream", "application/json", "application/x-tree" })]
// los gestores de archivos que dan la ruta: por la extensión, sea cual sea el tipo
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" }, DataHost = "*", DataMimeType = "*/*",
    DataPathPatterns = new[] { ".*\\.tree", ".*\\..*\\.tree", ".*\\..*\\..*\\.tree", ".*\\..*\\..*\\..*\\.tree" })]
// «Compartir con»
[IntentFilter(new[] { Intent.ActionSend, Intent.ActionSendMultiple }, Categories = new[] { Intent.CategoryDefault },
    DataMimeTypes = new[] { "application/octet-stream", "application/json", "application/x-tree" })]
public class MainActivity : AvaloniaMainActivity
{
    public static MainActivity? Actual { get; private set; }
    private const int PedirFoto = 4711;
    private TaskCompletionSource<bool>? _foto;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Actual = this;
        base.OnCreate(savedInstanceState);
        // si se recrea (por ejemplo, al volver del fondo) el archivo ya se atendió
        if (savedInstanceState == null) Atender(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Atender(intent);
    }

    protected override void OnDestroy()
    {
        if (Actual == this) Actual = null;
        base.OnDestroy();
    }

    private void Atender(Intent? intent)
    {
        if (intent == null) return;
        var uris = new List<Uri>();
        if (intent.Action == Intent.ActionView && intent.Data != null) uris.Add(intent.Data);
        else if (intent.Action == Intent.ActionSend && Extra(intent) is { } u) uris.Add(u);
        else if (intent.Action == Intent.ActionSendMultiple)
        {
#pragma warning disable CA1422, CS0618 // la forma nueva solo existe desde Android 13
            var lista = intent.GetParcelableArrayListExtra(Intent.ExtraStream);
#pragma warning restore CA1422, CS0618
            if (lista != null) foreach (var o in lista) if (o is Uri x) uris.Add(x);
        }
        foreach (var uri in uris)
        {
            try
            {
                using var s = ContentResolver!.OpenInputStream(uri);
                if (s == null) continue;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                App.Recibir(new ArchivoRecibido(Nombre(uri), ms.ToArray()));
            }
            catch (Exception ex) { global::Android.Util.Log.Warn("ArbolGenealogico", "No se pudo leer " + uri + ": " + ex.Message); }
        }
        // que al volver a la aplicación no se repita
        intent.SetAction(Intent.ActionMain);
        intent.SetData(null);
        intent.RemoveExtra(Intent.ExtraStream);
    }

    private static Uri? Extra(Intent intent)
    {
#pragma warning disable CA1422, CS0618
        return intent.GetParcelableExtra(Intent.ExtraStream) as Uri;
#pragma warning restore CA1422, CS0618
    }

    /// <summary>El nombre con el que se ve el archivo («Familia García.tree»), si la otra aplicación lo dice.</summary>
    private string Nombre(Uri uri)
    {
        try
        {
            using var c = ContentResolver!.Query(uri, new[] { IOpenableColumns.DisplayName }, null, null, null);
            if (c != null && c.MoveToFirst()) { var n = c.GetString(0); if (!string.IsNullOrWhiteSpace(n)) return n; }
        }
        catch { }
        return uri.LastPathSegment ?? "arbol.tree";
    }

    /// <summary>Abre la cámara del sistema para hacer una foto que se guarda en <paramref name="destino"/>.</summary>
    public Task<bool> HacerFoto(Uri destino)
    {
        _foto?.TrySetResult(false);
        _foto = new TaskCompletionSource<bool>();
        var i = new Intent(MediaStore.ActionImageCapture);
        i.PutExtra(MediaStore.ExtraOutput, destino);
        i.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
        try { StartActivityForResult(i, PedirFoto); }
        catch (ActivityNotFoundException) { _foto.TrySetResult(false); }
        return _foto.Task;
    }

    private readonly Dictionary<int, TaskCompletionSource<(Result, Intent?)>> _pendientes = new();
    private int _siguienteCodigo = 5000;

    /// <summary>Abre otra pantalla del sistema (p. ej. el selector de carpetas) y espera su resultado.</summary>
    public Task<(Result Resultado, Intent? Datos)> PedirResultado(Intent intent)
    {
        int codigo = _siguienteCodigo++;
        var t = new TaskCompletionSource<(Result, Intent?)>();
        _pendientes[codigo] = t;
        try { StartActivityForResult(intent, codigo); }
        catch (ActivityNotFoundException) { _pendientes.Remove(codigo); t.TrySetResult((Result.Canceled, null)); }
        return t.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == PedirFoto) _foto?.TrySetResult(resultCode == Result.Ok);
        else if (_pendientes.Remove(requestCode, out var t)) t.TrySetResult((resultCode, data));
    }
}
