using Android.Content;
using Android.OS;
using Android.Print;
using ArbolGenealogico.Core.Presentacion;

namespace ArbolGenealogico.Movil.Android;

/// <summary>
/// Imprime un PDF ya hecho con el sistema de impresión de Android (las impresoras de la red o por Wi-Fi Direct, o «Guardar
/// como PDF»): se abre su pantalla, con el papel y la orientación de las hojas ya elegidos.
/// </summary>
internal static class Impresora
{
    public static void Imprimir(Context contexto, string rutaPdf, string titulo, Papel papel, bool vertical, int paginas)
    {
        // el servicio de impresión necesita una actividad (abre su propia pantalla encima)
        var actividad = MainActivity.Actual ?? contexto;
        if (actividad.GetSystemService(Context.PrintService) is not PrintManager impresion)
            throw new InvalidOperationException("Este teléfono no tiene servicio de impresión.");
        var tamano = papel.Codigo switch
        {
            "A3" => PrintAttributes.MediaSize.IsoA3, "A2" => PrintAttributes.MediaSize.IsoA2, "A1" => PrintAttributes.MediaSize.IsoA1,
            "Letter" => PrintAttributes.MediaSize.NaLetter, "Legal" => PrintAttributes.MediaSize.NaLegal,
            "Tabloid" => PrintAttributes.MediaSize.NaTabloid, _ => PrintAttributes.MediaSize.IsoA4,
        };
        var atributos = new PrintAttributes.Builder()
            .SetMediaSize(vertical ? tamano!.AsPortrait() : tamano!.AsLandscape())!
            .Build();
        impresion.Print(titulo, new Documento(rutaPdf, titulo, paginas), atributos);
    }

    /// <summary>Le da al sistema el PDF tal cual (sus páginas ya tienen el tamaño del papel).</summary>
    private sealed class Documento(string ruta, string titulo, int paginas) : PrintDocumentAdapter
    {
        public override void OnLayout(PrintAttributes? antes, PrintAttributes? ahora, CancellationSignal? cancelar,
            LayoutResultCallback? respuesta, Bundle? extras)
        {
            if (cancelar?.IsCanceled == true) { respuesta?.OnLayoutCancelled(); return; }
            var info = new PrintDocumentInfo.Builder(Path.GetFileName(ruta))
                .SetContentType(PrintContentType.Document)!
                .SetPageCount(paginas)!
                .Build();
            respuesta?.OnLayoutFinished(info, !Equals(antes, ahora));
        }

        public override void OnWrite(PageRange[]? paginasPedidas, ParcelFileDescriptor? destino, CancellationSignal? cancelar,
            WriteResultCallback? respuesta)
        {
            try
            {
                using var salida = new Java.IO.FileOutputStream(destino!.FileDescriptor);
                using var entrada = File.OpenRead(ruta);
                var trozo = new byte[64 * 1024];
                int n;
                while ((n = entrada.Read(trozo, 0, trozo.Length)) > 0)
                {
                    if (cancelar?.IsCanceled == true) { respuesta?.OnWriteCancelled(); return; }
                    salida.Write(trozo, 0, n);
                }
                respuesta?.OnWriteFinished(new[] { PageRange.AllPages! });
            }
            catch (Exception ex) { respuesta?.OnWriteFailed(ex.Message); }
        }
    }
}
