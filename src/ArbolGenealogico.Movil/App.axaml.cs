using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ArbolGenealogico.Movil.Vistas;

namespace ArbolGenealogico.Movil;

public partial class App : Application
{
    /// <summary>Lo que depende del sistema (compartir, carpeta de datos…). Lo pone cada plataforma al arrancar.</summary>
    public static IPlataforma Plataforma { get; set; } = new PlataformaGenerica();

    private static Biblioteca? _biblioteca;
    public static Biblioteca Biblioteca => _biblioteca ??= new Biblioteca(Plataforma.CarpetaDatos);

    private static readonly Queue<ArchivoRecibido> _recibidos = new();
    private static Principal? _principal;

    /// <summary>
    /// Un archivo que ha llegado de fuera (abierto desde WhatsApp, el correo, el gestor de archivos, «Compartir»…). Si la
    /// aplicación aún se está abriendo, espera a que esté lista.
    /// </summary>
    public static void Recibir(ArchivoRecibido archivo)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _recibidos.Enqueue(archivo);
            _principal?.AtenderRecibidos();
        });
    }

    internal static ArchivoRecibido? SiguienteRecibido() => _recibidos.Count > 0 ? _recibidos.Dequeue() : null;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Aplica el tamaño de letra del sistema a los textos de la aplicación (también a los de los controles de Fluent).</summary>
    private void AplicarEscalaLetra()
    {
        Ui.Escala = Math.Clamp(Plataforma.EscalaLetra, 0.85, 2.2);
        Resources["TamOpcion"] = Ui.L(16);
        Resources["TamCampo"] = Ui.L(17);
        Resources["ControlContentThemeFontSize"] = Ui.L(15);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Principal Crear()
        {
            // el tamaño de letra del sistema, cada vez que se crea la vista: al cambiarlo en Ajustes, Android la vuelve a crear
            AplicarEscalaLetra();
            _principal = new Principal();
            return _principal;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime escritorio)
            escritorio.MainWindow = new Avalonia.Controls.Window
            {
                Title = "Ramo", Width = 412, Height = 860, Content = Crear(),
                Background = Avalonia.Media.Brushes.Black,
            };
        else if (ApplicationLifetime is IActivityApplicationLifetime actividad)
            actividad.MainViewFactory = Crear;
        else if (ApplicationLifetime is ISingleViewApplicationLifetime vista)
            vista.MainView = Crear();
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>Un archivo recibido: su nombre (si se sabe) y su contenido.</summary>
public sealed record ArchivoRecibido(string Nombre, byte[] Datos);
