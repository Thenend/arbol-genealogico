using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ArbolGenealogico.Movil;
using ArbolGenealogico.Movil.Vistas;

namespace ArbolGenealogico.Movil.Escritorio;

/// <summary>
/// La aplicación del móvil en una ventana con el tamaño de un teléfono. Para ver cómo queda sin tocarla:
/// <c>--captura salida.png [ancho alto] [pasos…]</c>, con pasos como <c>recibir:ruta.ramo</c>, <c>abrir:ruta.ramo</c>,
/// <c>sel:Nombre</c>, <c>ficha:Nombre</c>, <c>familiar:Nombre</c>, <c>menu</c>, <c>buscar:texto</c>, <c>zoom:1.5</c> (multiplica), <c>escala:0.19</c> (fija la escala), <c>anotar:archivo</c> (añade la escala a un archivo),
/// <c>esperar:ms</c>, <c>foto:salida.png</c> (una captura intermedia) y <c>pdf:salida.pdf</c> (lo que manda «Compartir como PDF»), <c>foto-persona:imagen.jpg</c> (abre el encuadre de una foto),
/// <c>encuadre-mover:dx,dy</c>, <c>encuadre-ampliar:1.5</c> y <c>exportar-ged:salida.ged</c>. Con la variable ARBOL_DATOS se usa otra carpeta de datos.
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("ARBOL_DATOS") is { Length: > 0 } datos)
            App.Plataforma = new PlataformaPrueba(datos);
        var i = Array.IndexOf(args, "--captura");
        if (i < 0) return Construir().StartWithClassicDesktopLifetime(args);

        var salida = args[i + 1];
        int ancho = 412, alto = 860, k = i + 2;
        if (args.Length > k + 1 && int.TryParse(args[k], out var w) && int.TryParse(args[k + 1], out var h)) { ancho = w; alto = h; k += 2; }
        var pasos = args.Skip(k).ToList();
        return Construir().AfterSetup(_ => Dispatcher.UIThread.Post(async () =>
        {
            var vida = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            var ventana = vida.MainWindow!;
            ventana.Width = ancho; ventana.Height = alto;
            int codigo = 0;
            try
            {
                await Task.Delay(700);
                foreach (var paso in pasos) await Paso(ventana, paso);
                await Task.Delay(500);
                Capturar(ventana, salida);
            }
            catch (Exception ex) { File.WriteAllText(salida + ".error.txt", ex.ToString()); codigo = 2; }
            vida.Shutdown(codigo);
        }, DispatcherPriority.Background)).StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder Construir() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();

    private static void Capturar(Window ventana, string ruta)
    {
        var c = (Control)ventana.Content!;
        // ARBOL_ESCALA: más píxeles por punto (p. ej. 2.5 para las capturas de Google Play, 1080 × 2160 con 432 × 864)
        double k = double.TryParse(Environment.GetEnvironmentVariable("ARBOL_ESCALA"), System.Globalization.CultureInfo.InvariantCulture, out var e) ? e : 1;
        var tam = new PixelSize((int)Math.Round(c.Bounds.Width * k), (int)Math.Round(c.Bounds.Height * k));
        using var rtb = new RenderTargetBitmap(tam, new Vector(96 * k, 96 * k));
        rtb.Render(c);
        rtb.Save(ruta);
    }

    /// <summary>Abre el encuadre con una imagen del disco, como si se acabara de elegir en la galería.</summary>
    private static async Task PonerFotoPrueba(string ruta)
    {
        var img = Fotos.Abrir(File.ReadAllBytes(ruta))!;
        // con «Usar», guarda al lado la foto que se pondría a la persona
        if (await EncuadrarFoto.Abrir(img) is { } r)
            File.WriteAllBytes(ruta + ".recorte.jpg", Convert.FromBase64String(Fotos.Recortar(img, r)));
    }

    private static async Task Paso(Window ventana, string paso)
    {
        var (orden, valor) = paso.IndexOf(':') is var j and > 0 ? (paso[..j], paso[(j + 1)..]) : (paso, "");
        var pantalla = Principal.Actual.Pantalla as PantallaArbol;
        string Id(string nombre) => pantalla!.Lienzo.Arbol!.Personas.First(p => p.NombreCompleto.StartsWith(nombre, StringComparison.OrdinalIgnoreCase)).Id;
        switch (orden)
        {
            case "recibir": App.Recibir(new ArchivoRecibido(Path.GetFileName(valor), File.ReadAllBytes(valor))); break;
            case "abrir":
                var a = Core.Io.Gedcom.EsGedcom(valor)
                    ? Core.Io.Gedcom.Importar(File.ReadAllBytes(valor), Path.GetFileName(valor), out _)
                    : Core.Io.ArbolJson.Cargar(valor, out _);
                PantallaInicio.Abrir(App.Biblioteca.Importar(a, Path.GetFileName(valor)));
                break;
            case "sel": pantalla!.Lienzo.Seleccion = Id(valor); pantalla.Lienzo.CentrarEn(Id(valor), null, false); break;
            case "ficha": _ = pantalla!.Prueba("ficha", Id(valor)); break;
            case "familiar": _ = pantalla!.Prueba("familiar", Id(valor)); break;
            case "menu": _ = pantalla!.Prueba("menu", null); break;
            case "buscar": _ = pantalla!.Prueba("buscar:" + valor, null); break;
            case "escala": pantalla!.Lienzo.Zoom(double.Parse(valor, System.Globalization.CultureInfo.InvariantCulture) / pantalla.Lienzo.Escala); break;
            case "anotar": File.AppendAllText(valor, pantalla!.Lienzo.Escala.ToString(System.Globalization.CultureInfo.InvariantCulture) + Environment.NewLine); break;
            case "foto-persona": _ = PonerFotoPrueba(valor); break;
            case "encuadre-mover": { var xy = valor.Split(','); EncuadrarFoto.Actual!.Mover(double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)); break; }
            case "verfoto": VerFoto.Abrir(pantalla!.Lienzo.Arbol!.Obtener(Id(valor))); break;
            case "encuadre-usar": EncuadrarFoto.Usar!(); break;
            case "encuadre-ampliar": EncuadrarFoto.Actual!.Ampliar(double.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)); break;
            case "exportar-ged": File.WriteAllBytes(valor, Core.Io.Gedcom.Exportar(pantalla!.Lienzo.Arbol!, Path.GetFileName(valor))); break;
            case "zoom": pantalla!.Lienzo.Zoom(double.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)); break;
            case "fusionar": _ = PantallaFusion.Fusionar(pantalla!.Ruta, Core.Io.ArbolJson.Cargar(valor, out _), Path.GetFileNameWithoutExtension(valor)); break;
            case "orden": pantalla!.Lienzo.Ordenacion = Enum.Parse<Core.Layout.Ordenacion>(valor); break;
            case "forma": pantalla!.Lienzo.Forma = Core.Presentacion.FormaTarjeta.Leer(valor.Replace(',', ' ')); break;
            case "mover": { var xy = valor.Split(','); pantalla!.Lienzo.Mover(double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)); break; }
            case "dobletoque": { var xy = valor.Split(','); pantalla!.Lienzo.AlternarZoom(new Point(double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture))); break; }
            case "pdf": await pantalla!.Lienzo.GuardarPdf(valor, "prueba"); break;
            case "todo": pantalla!.Lienzo.Encuadrar(false); break;
            case "esperar": await Task.Delay(int.Parse(valor)); break;
            case "foto": await Task.Delay(400); Capturar(ventana, valor); break;
            default: throw new ArgumentException("Paso desconocido: " + paso);
        }
        await Task.Delay(350);
    }
}

internal sealed class PlataformaPrueba(string carpeta) : IPlataforma
{
    public string CarpetaDatos => carpeta;
    // ARBOL_LETRA=1.5: como el tamaño de fuente de Android
    public double EscalaLetra => double.TryParse(Environment.GetEnvironmentVariable("ARBOL_LETRA"), System.Globalization.CultureInfo.InvariantCulture, out var e) ? e : 1;
    public Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo)
    {
        File.WriteAllLines(Path.Combine(carpeta, "compartido.txt"), rutas.Prepend(tipo + " | " + titulo));
        return Task.CompletedTask;
    }
}
