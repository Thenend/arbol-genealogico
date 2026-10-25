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
/// <c>sel:Nombre</c>, <c>ficha:Nombre</c>, <c>familiar:Nombre</c>, <c>parentesco:Nombre</c> (y luego <c>sel:Otro</c> para tocar a la otra persona), <c>menu</c>, <c>buscar:texto</c>, <c>zoom:1.5</c> (multiplica), <c>escala:0.19</c> (fija la escala), <c>anotar:archivo</c> (añade la escala a un archivo),
/// <c>tema:album</c> (los colores), <c>letra:caveat</c> (la letra de las tarjetas), <c>esperar:ms</c>, <c>foto:salida.png</c> (una captura intermedia) y <c>pdf:salida.pdf</c> (lo que manda «Compartir como PDF»), <c>foto-persona:imagen.jpg</c> (abre el encuadre de una foto),
/// <c>encuadre-mover:dx,dy</c>, <c>encuadre-ampliar:1.5</c> y <c>exportar-ged:salida.ged</c>. Con la variable ARBOL_DATOS se usa otra carpeta de datos, y con ARBOL_IDIOMA (en, fr, it, de, pt) otro idioma.
/// La versión completa: <c>completa</c> (abre «Ramo completo»), <c>nuevo</c> («Nuevo árbol»), <c>pulsar:Texto</c> (el botón
/// con ese texto) e <c>imagen:salida.png</c> (lo que manda «Compartir como imagen»); ARBOL_COMPLETA=1 empieza con ella
/// comprada y ARBOL_COMPRA (pendiente, cancelada, fallida) dice cómo acaba la compra.
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("ARBOL_DATOS") is { Length: > 0 } datos)
            App.Plataforma = new PlataformaPrueba(datos);
        // ARBOL_PC=1: la ventana del ordenador (barra de herramientas, minimapa, menús con el botón derecho)
        App.Escritorio = Environment.GetEnvironmentVariable("ARBOL_PC") == "1";
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
        // a 96 ppp y con la escala como transformación: con más ppp, Avalonia pierde la transformación del lienzo después de
        // dibujar una foto (las tarjetas siguientes salen sin texto, desplazadas o enormes)
        using var rtb = new RenderTargetBitmap(tam, new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        using (ctx.PushTransform(Matrix.CreateScale(k, k)))
            ctx.DrawRectangle(new Avalonia.Media.VisualBrush(c) { Stretch = Avalonia.Media.Stretch.None, AlignmentX = Avalonia.Media.AlignmentX.Left,
                AlignmentY = Avalonia.Media.AlignmentY.Top }, null, new Rect(c.Bounds.Size));
        rtb.Save(ruta);
    }

    private static async Task CapturarPantalla(Window ventana, string ruta)
    {
        var p = ventana.PointToScreen(new Point(0, 0));
        var t = ventana.ClientSize;
        var info = new System.Diagnostics.ProcessStartInfo("import", $"-window root -crop {(int)t.Width}x{(int)t.Height}+{p.X}+{p.Y} +repage \"{ruta}\"");
        using var proc = System.Diagnostics.Process.Start(info)!;
        await proc.WaitForExitAsync();
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
        if (Principal.Actual.Pantalla is Ordenador.PantallaPc pc && orden is not ("esperar" or "foto" or "pantalla" or "tema" or "letra" or "pulsar" or "final"))
        {
            // en la ventana del ordenador: pc-… para lo de la barra y los menús (abrir, sel, ficha, menu, anadir, aspecto…)
            if (orden == "pc") { var kv = valor.Split('=', 2); await pc.Prueba(kv[0], kv.Length > 1 ? kv[1] : ""); }
            else if (orden == "imprimir" && valor.Length > 0) { var kv = valor.Split('=', 2); await PantallaImpresion.Actual!.Prueba(kv[0], kv[1]); }
            else await pc.Prueba(orden, valor);
            await Task.Delay(350);
            return;
        }
        var pantalla = Principal.Actual.Pantalla as PantallaArbol;
        string Id(string nombre) => pantalla!.Lienzo.Arbol!.Personas.First(p => p.NombreCompleto.StartsWith(nombre, StringComparison.OrdinalIgnoreCase)).Id;
        switch (orden)
        {
            case "ejemplo": await ((PantallaInicio)Principal.Actual.Pantalla!).AbrirEjemplo(); break;
            case "info": _ = ((PantallaInicio)Principal.Actual.Pantalla!).ComoCompartir(); break;
            case "nuevo": _ = ((PantallaInicio)Principal.Actual.Pantalla!).Nuevo(); break;
            case "completa": _ = PantallaCompleta.Ofrecer(); break;
            // pulsa el botón (el último, el de más arriba en la pantalla) cuyo texto empieza así
            case "pulsar":
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(ventana).OfType<Button>()
                    .Last(b => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(b).OfType<TextBlock>().Any(t => t.Text?.StartsWith(valor) == true))
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                break;
            case "imagen": using (var img = pantalla!.Lienzo.Imagen(1, Licencia.ConMarca)) img.Save(valor); break;
            // los árboles de ejemplo (y los hechos a partir de ellos) salen en el idioma de ahora, como en la aplicación
            case "recibir":
                App.Recibir(new ArchivoRecibido(Path.GetFileName(valor), Core.Io.Gedcom.EsGedcom(valor) ? File.ReadAllBytes(valor)
                    : System.Text.Encoding.UTF8.GetBytes(Core.Io.ArbolJson.Serializar(PantallaInicio.TraducirEjemplo(Core.Io.ArbolJson.Cargar(valor, out _))))));
                break;
            case "abrir":
                var a = Core.Io.Gedcom.EsGedcom(valor)
                    ? Core.Io.Gedcom.Importar(File.ReadAllBytes(valor), Path.GetFileName(valor), out _)
                    : PantallaInicio.TraducirEjemplo(Core.Io.ArbolJson.Cargar(valor, out _));
                PantallaInicio.Abrir(App.Biblioteca.Importar(a, Path.GetFileName(valor)));
                break;
            case "inicio": Principal.Actual.Ir(new PantallaInicio()); break;
            // pulsa el botón con dibujo número n del menú abierto (0, 1… de arriba abajo y de izquierda a derecha)
            case "mosaico":
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(ventana).OfType<Button>().Where(b => b.Classes.Contains("mosaico"))
                    .ElementAt(int.Parse(valor)).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                break;
            // lleva al final lo que se desplaza (p. ej. el menú abierto, para ver sus últimas opciones)
            case "final": foreach (var sv in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(ventana).OfType<ScrollViewer>()) sv.ScrollToEnd(); break;
            case "sel": pantalla!.Lienzo.Seleccion = Id(valor); pantalla.Lienzo.CentrarEn(Id(valor), null, false); break;
            case "ficha": _ = pantalla!.Prueba("ficha", Id(valor)); break;
            case "familiar": _ = pantalla!.Prueba("familiar", Id(valor)); break;
            case "parentesco": _ = pantalla!.Prueba("parentesco", Id(valor)); break;
            case "menu": _ = pantalla!.Prueba("menu", null); break;
            // imprimir: abre la pantalla de imprimir; imprimir:papel=Letter, imprimir:hojas=3, imprimir:estilo=Oscuro,
            // imprimir:cortes=iguales, imprimir:pdf=ruta (guarda las hojas)
            case "imprimir":
                if (valor.Length == 0) _ = pantalla!.Prueba("imprimir", null);
                else { var kv = valor.Split('=', 2); await PantallaImpresion.Actual!.Prueba(kv[0], kv[1]); }
                break;
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
            // fusionar:ruta|Ana: el árbol recibido se llama «El árbol de Ana» (en el idioma de ahora)
            case "fusionar":
                var (rutaOtro, de) = valor.Split('|') is { Length: 2 } ps ? (ps[0], ps[1]) : (valor, null);
                var otro = PantallaInicio.TraducirEjemplo(Core.Io.ArbolJson.Cargar(rutaOtro, out _));
                _ = PantallaFusion.Fusionar(pantalla!.Ruta, otro, de == null ? Path.GetFileNameWithoutExtension(rutaOtro) : Core.Idiomas.Idioma.T("El árbol de {0}", de));
                break;
            case "orden": pantalla!.Lienzo.Ordenacion = Enum.Parse<Core.Layout.Ordenacion>(valor); break;
            // tema:dia, tema:album, tema:jardin, tema:contraste o tema:noche (sin guardarlo en las preferencias)
            case "tema": Dibujo.Tema.Poner(Dibujo.Tema.Leer(valor)); break;
            // letra:barlow, letra:lora, letra:atkinson, letra:nunito, letra:merriweather o letra:caveat (sin guardarla)
            case "letra": Dibujo.Letra.Poner(Dibujo.Letra.Leer(valor)); break;
            case "forma": pantalla!.Lienzo.Forma = Core.Presentacion.FormaTarjeta.Leer(valor.Replace(',', ' ')); break;
            case "mover": { var xy = valor.Split(','); pantalla!.Lienzo.Mover(double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)); break; }
            case "dobletoque": { var xy = valor.Split(','); pantalla!.Lienzo.AlternarZoom(new Point(double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture))); break; }
            case "pdf": await pantalla!.Lienzo.GuardarPdf(valor, "prueba", Licencia.ConMarca); break;
            case "todo": pantalla!.Lienzo.Encuadrar(false); break;
            case "esperar": await Task.Delay(int.Parse(valor)); break;
            case "foto": await Task.Delay(400); Capturar(ventana, valor); break;
            // pantalla:salida.png: lo que se ve en la pantalla (con los menús que se abren encima), recortado a la ventana
            case "pantalla": await Task.Delay(600); await CapturarPantalla(ventana, valor); break;
            default: throw new ArgumentException("Paso desconocido: " + paso);
        }
        await Task.Delay(350);
    }
}

internal sealed class PlataformaPrueba(string carpeta) : IPlataforma
{
    public string CarpetaDatos => carpeta;
    public ITienda? Tienda { get; } = new TiendaPrueba();
    // ARBOL_LETRA=1.5: como el tamaño de fuente de Android
    public double EscalaLetra => double.TryParse(Environment.GetEnvironmentVariable("ARBOL_LETRA"), System.Globalization.CultureInfo.InvariantCulture, out var e) ? e : 1;
    // ARBOL_IDIOMA=en: como el idioma del teléfono
    public string Idioma => Environment.GetEnvironmentVariable("ARBOL_IDIOMA") is { Length: > 0 } i ? i : System.Globalization.CultureInfo.CurrentUICulture.Name;
    public Task Compartir(IReadOnlyList<string> rutas, string tipo, string titulo)
    {
        File.WriteAllLines(Path.Combine(carpeta, "compartido.txt"), rutas.Prepend(tipo + " | " + titulo));
        return Task.CompletedTask;
    }
}
