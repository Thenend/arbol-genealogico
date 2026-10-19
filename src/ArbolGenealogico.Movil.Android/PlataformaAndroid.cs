using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using ArbolGenealogico.Core.Io;
using JavaFile = Java.IO.File;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Android;

/// <summary>Android: los árboles en la carpeta privada de la aplicación (entra en la copia de seguridad de Google) y el menú «Compartir» del sistema.</summary>
public sealed class PlataformaAndroid(Context contexto) : IPlataforma
{
    private string Autoridad => contexto.PackageName + ".archivos";

    public string CarpetaDatos => contexto.FilesDir!.AbsolutePath;

    /// <summary>Ajustes → Pantalla → Tamaño de fuente (1 = normal; hasta 2 en Android 14).</summary>
    public double EscalaLetra => contexto.Resources?.Configuration?.FontScale is float f and > 0 ? f : 1;

    /// <summary>El idioma del teléfono, o el elegido solo para esta aplicación (Ajustes → Aplicaciones → Ramo → Idioma, desde Android 13).</summary>
    public string Idioma =>
        ((MainActivity.Actual as Context ?? contexto).Resources?.Configuration?.Locales.Get(0) ?? Java.Util.Locale.Default)?.ToLanguageTag() ?? "en";

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
        // esta misma aplicación abre los archivos genéricos (los .ramo), pero no tiene sentido mandárselos a sí misma
        selector.PutExtra(Intent.ExtraExcludeComponents, new IParcelable[] { new ComponentName(contexto, Java.Lang.Class.FromType(typeof(MainActivity))) });
        selector.AddFlags(ActivityFlags.NewTask);
        (MainActivity.Actual ?? contexto).StartActivity(selector);
        return Task.CompletedTask;
    }

    public Task Imprimir(string rutaPdf, string titulo, Core.Presentacion.Papel papel, bool vertical, int hojas)
    {
        Impresora.Imprimir(contexto, rutaPdf, titulo, papel, vertical, hojas);
        return Task.CompletedTask;
    }

    // ---------- Copia de seguridad en Documentos ----------
    // Desde Android 10, una aplicación puede crear y reescribir sus propios archivos en Documentos sin pedir permisos
    // (MediaStore). Esos archivos no se borran al desinstalar la aplicación ni al borrar sus datos, y se ven en «Archivos».

    private const string SubcarpetaCopia = "Ramo";
    private static string RutaRelativaCopia => global::Android.OS.Environment.DirectoryDocuments + "/" + SubcarpetaCopia + "/";

    // como la ve el usuario: «Documentos/Ramo» en español; la carpeta es la misma («Documents»)
    public string? CarpetaCopia => OperatingSystem.IsAndroidVersionAtLeast(29) ? T("Documentos") + "/" + SubcarpetaCopia : null;

    public Task GuardarCopia(string nombreArchivo, byte[] datos) => Task.Run(() =>
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) return;
        var resolver = contexto.ContentResolver!;
        global::Android.Net.Uri? uri;
        if (CarpetaConcedida() is { } carpeta)
        {
            // con permiso sobre la carpeta (se da al recuperar), se reescribe la copia aunque la escribiera una
            // instalación anterior: sin él, Android no deja verla y se crearía «nombre (1).ramo»
            uri = HijoDe(carpeta, nombreArchivo) ?? DocumentsContract.CreateDocument(resolver,
                DocumentsContract.BuildDocumentUriUsingTree(carpeta, DocumentsContract.GetTreeDocumentId(carpeta))!,
                "application/octet-stream", nombreArchivo);
        }
        else
        {
            uri = BuscarCopia(nombreArchivo);
            if (uri == null)
            {
                var valores = new ContentValues();
                valores.Put(MediaStore.IMediaColumns.DisplayName, nombreArchivo);
                valores.Put(MediaStore.IMediaColumns.MimeType, "application/octet-stream");
                valores.Put(MediaStore.IMediaColumns.RelativePath, RutaRelativaCopia);
                uri = resolver.Insert(Coleccion, valores);
            }
        }
        if (uri == null) throw new IOException("No se pudo crear la copia en Documentos.");
        // "wt": se reescribe entera (sin restos si la nueva es más corta)
        using var s = resolver.OpenOutputStream(uri, "wt") ?? throw new IOException("No se pudo escribir la copia en Documentos.");
        s.Write(datos, 0, datos.Length);
    });

    public Task BorrarCopia(string nombreArchivo) => Task.Run(() =>
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29)) return;
        if (CarpetaConcedida() is { } carpeta)
        {
            if (HijoDe(carpeta, nombreArchivo) is { } doc) DocumentsContract.DeleteDocument(contexto.ContentResolver!, doc);
        }
        else if (BuscarCopia(nombreArchivo) is { } uri) contexto.ContentResolver!.Delete(uri, null, null);
    });

    public bool PuedeLeerCopias => OperatingSystem.IsAndroidVersionAtLeast(29);

    /// <summary>El id de la carpeta de la copia para el proveedor de archivos de Android.</summary>
    private static string IdCarpetaCopia => "primary:" + global::Android.OS.Environment.DirectoryDocuments + "/" + SubcarpetaCopia;
    private const string ClaveCarpeta = "carpeta_copia";

    public async Task<IReadOnlyList<ArchivoRecibido>?> LeerCopias()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29) || MainActivity.Actual is not { } actividad) return null;
        // el selector de carpetas del sistema, abierto ya en Documentos/Ramo: basta con «Usar esta carpeta»
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.PutExtra(DocumentsContract.ExtraInitialUri, DocumentsContract.BuildDocumentUri("com.android.externalstorage.documents", IdCarpetaCopia));
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
        var (resultado, datos) = await actividad.PedirResultado(intent);
        if (resultado != global::Android.App.Result.Ok || datos?.Data is not { } carpeta) return null;
        // si es la carpeta de la copia, el permiso se guarda: desde ahora las copias se escriben a través de ella
        if (DocumentsContract.GetTreeDocumentId(carpeta) == IdCarpetaCopia)
        {
            try
            {
                contexto.ContentResolver!.TakePersistableUriPermission(carpeta, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                contexto.GetSharedPreferences("arbol", FileCreationMode.Private)!.Edit()!.PutString(ClaveCarpeta, carpeta.ToString())!.Apply();
            }
            catch (Java.Lang.SecurityException) { }
        }
        return await Task.Run(() =>
        {
            var resolver = contexto.ContentResolver!;
            var lista = new List<ArchivoRecibido>();
            var hijos = Hijos(carpeta).ToList();
            var nombres = hijos.Select(h => h.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (nombre, uri) in hijos)
            {
                if (!ArbolJson.EsArchivoDeArbol(nombre)) continue;
                // la copia de antes (.tree) de un árbol que ya tiene la de ahora (.ramo) sobra
                if (!nombre.EndsWith(ArbolJson.Extension, StringComparison.OrdinalIgnoreCase)
                    && nombres.Contains(Path.ChangeExtension(nombre, ArbolJson.Extension))) continue;
                using var s = resolver.OpenInputStream(uri);
                if (s == null) continue;
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                lista.Add(new ArchivoRecibido(nombre, ms.ToArray()));
            }
            return (IReadOnlyList<ArchivoRecibido>)lista;
        });
    }

    /// <summary>La carpeta de la copia, si se dio permiso sobre ella y sigue vigente.</summary>
    private global::Android.Net.Uri? CarpetaConcedida()
    {
        var texto = contexto.GetSharedPreferences("arbol", FileCreationMode.Private)!.GetString(ClaveCarpeta, null);
        if (texto == null) return null;
        var uri = global::Android.Net.Uri.Parse(texto)!;
        // solo la carpeta de la copia de ahora (la de antes de llamarse «Ramo», «Árbol Genealógico», ya no se usa)
        if (DocumentsContract.GetTreeDocumentId(uri) != IdCarpetaCopia) return null;
        foreach (var p in contexto.ContentResolver!.PersistedUriPermissions)
            if (p.Uri != null && p.Uri.Equals(uri) && p.IsWritePermission) return uri;
        return null;
    }

    private IEnumerable<(string Nombre, global::Android.Net.Uri Uri)> Hijos(global::Android.Net.Uri carpeta)
    {
        var hijos = DocumentsContract.BuildChildDocumentsUriUsingTree(carpeta, DocumentsContract.GetTreeDocumentId(carpeta))!;
        var lista = new List<(string, global::Android.Net.Uri)>();
        using var c = contexto.ContentResolver!.Query(hijos, new[] { DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName }, null, null, null);
        while (c != null && c.MoveToNext())
            lista.Add((c.GetString(1) ?? "", DocumentsContract.BuildDocumentUriUsingTree(carpeta, c.GetString(0))!));
        return lista;
    }

    private global::Android.Net.Uri? HijoDe(global::Android.Net.Uri carpeta, string nombre) =>
        Hijos(carpeta).Where(h => h.Nombre == nombre).Select(h => h.Uri).FirstOrDefault();

    private static global::Android.Net.Uri Coleccion => MediaStore.Files.GetContentUri("external")!;

    /// <summary>La copia con ese nombre que escribió esta instalación (las de instalaciones anteriores no se ven sin permisos).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("android29.0")]
    private global::Android.Net.Uri? BuscarCopia(string nombreArchivo)
    {
        using var c = contexto.ContentResolver!.Query(Coleccion, new[] { IBaseColumns.Id },
            MediaStore.IMediaColumns.RelativePath + "=? AND " + MediaStore.IMediaColumns.DisplayName + "=?",
            new[] { RutaRelativaCopia, nombreArchivo }, null);
        if (c == null || !c.MoveToFirst()) return null;
        return ContentUris.WithAppendedId(Coleccion, c.GetLong(0));
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
