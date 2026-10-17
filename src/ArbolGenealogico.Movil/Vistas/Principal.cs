using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ArbolGenealogico.Core.Io;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>Una pantalla de la aplicación. <see cref="Atras"/> = true si ha atendido el botón «Atrás» del teléfono.</summary>
public interface IPantalla
{
    bool Atras();
}

/// <summary>
/// La vista raíz: la pantalla actual (la biblioteca o un árbol) y, encima, la capa de los diálogos (menús que suben desde
/// abajo, preguntas, avisos y hojas a pantalla completa como el editor). El botón «Atrás» cierra primero lo de encima.
/// </summary>
public sealed class Principal : Grid
{
    public static Principal Actual { get; private set; } = null!;

    private readonly ContentControl _contenido = new();
    private readonly Panel _capa = new();
    private readonly Border _aviso;
    private readonly TextBlock _avisoTexto = new() { TextWrapping = TextWrapping.Wrap, FontSize = Ui.L(15), Foreground = Brushes.White };
    private readonly List<(Control Vista, Action Cancelar)> _abiertos = new();
    private DispatcherTimer? _avisoTimer;

    public Principal()
    {
        Actual = this;
        Background = Ui.Fondo;
        Avalonia.Controls.Documents.TextElement.SetFontSize(this, Ui.L(15));
        _aviso = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x48)), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12), Margin = new Thickness(16, 0, 16, 28), MaxWidth = 520,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Child = _avisoTexto, IsVisible = false, IsHitTestVisible = false,
            BorderBrush = Ui.Linea, BorderThickness = new Thickness(1),
        };
        Children.Add(_contenido);
        Children.Add(_capa);
        Children.Add(_aviso);
        Ir(new PantallaInicio());
        AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is { } tl)
            {
                tl.BackRequested -= AlPedirAtras;
                tl.BackRequested += AlPedirAtras;
                // En el teléfono, que el contenido no quede debajo de la barra de estado, de la de navegación ni del
                // teclado: la aplicación ocupa toda la pantalla, así que el sitio se deja con márgenes.
                var seguro = new Thickness();
                double teclado = 0;
                void Margenes() => Margin = new Thickness(seguro.Left, seguro.Top, seguro.Right, Math.Max(seguro.Bottom, teclado));
                if (tl.InsetsManager is { } im)
                {
                    im.DisplayEdgeToEdgePreference = true;
                    im.SafeAreaChanged += (_, _) => { seguro = im.SafeAreaPadding; Margenes(); };
                    seguro = im.SafeAreaPadding;
                    Margenes();
                }
                if (tl.InputPane is { } ip)
                {
                    ip.StateChanged += (_, e) =>
                    {
                        teclado = e.NewState == InputPaneState.Open ? Math.Max(0, tl.Bounds.Height - e.EndRect.Top) : 0;
                        Margenes();
                        // al encogerse la pantalla, el campo en el que se escribe se desplaza para quedar a la vista
                        if (teclado > 0)
                            Dispatcher.UIThread.Post(() => (tl.FocusManager?.GetFocusedElement() as Control)?.BringIntoView(), DispatcherPriority.Background);
                    };
                }
            }
            Dispatcher.UIThread.Post(AtenderRecibidos, DispatcherPriority.Background);
            // que todos los árboles tengan su copia en Documentos (también los guardados antes de que existiera)
            Dispatcher.UIThread.Post(() => App.Biblioteca.CopiarTodos(), DispatcherPriority.Background);
        };
    }

    public Control? Pantalla => _contenido.Content as Control;

    public void Ir(Control pantalla)
    {
        CerrarTodo();
        _contenido.Content = pantalla;
    }

    private void AlPedirAtras(object? s, RoutedEventArgs e)
    {
        if (_abiertos.Count > 0) { _abiertos[^1].Cancelar(); e.Handled = true; return; }
        if (_contenido.Content is IPantalla p && p.Atras()) e.Handled = true;
    }

    // ---------- Archivos recibidos ----------

    private bool _atendiendo;

    /// <summary>Abre los árboles que han llegado de fuera (uno detrás de otro).</summary>
    public async void AtenderRecibidos()
    {
        if (_atendiendo) return;
        _atendiendo = true;
        try
        {
            while (App.SiguienteRecibido() is { } r) await Recepcion.Atender(r);
        }
        finally { _atendiendo = false; }
    }

    // ---------- Capa de diálogos ----------

    private Task<T> Abrir<T>(Func<Action<T>, Control> crear, T cancelado, bool desdeAbajo, bool cerrarAlTocarFuera = true, bool completa = false)
    {
        var tcs = new TaskCompletionSource<T>();
        Control? vista = null;
        Border? velo = null;
        void Cerrar(T valor)
        {
            if (tcs.Task.IsCompleted) return;
            _capa.Children.Remove(velo!);
            _abiertos.RemoveAll(a => a.Vista == velo);
            tcs.SetResult(valor);
        }
        vista = crear(Cerrar);
        velo = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)),
            Child = vista,
        };
        vista.HorizontalAlignment = desdeAbajo || completa ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        vista.VerticalAlignment = completa ? VerticalAlignment.Stretch : desdeAbajo ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        velo.PointerPressed += (_, e) =>
        {
            if (cerrarAlTocarFuera && e.Source == velo) { Cerrar(cancelado); e.Handled = true; }
        };
        _capa.Children.Add(velo);
        _abiertos.Add((velo, () => Cerrar(cancelado)));
        return tcs.Task;
    }

    private static Border Tarjeta(Control contenido, bool desdeAbajo) => new()
    {
        Background = Ui.Panel,
        CornerRadius = desdeAbajo ? new CornerRadius(20, 20, 0, 0) : new CornerRadius(18),
        Padding = desdeAbajo ? new Thickness(0, 10, 0, 18) : new Thickness(22, 20, 22, 14),
        Margin = desdeAbajo ? new Thickness(0) : new Thickness(24),
        MaxWidth = desdeAbajo ? 640 : 460,
        Child = contenido,
    };

    /// <summary>Una pregunta con botones (el primero es el principal). Devuelve el botón pulsado, o -1 si se cancela.</summary>
    public Task<int> Preguntar(string titulo, string texto, params string[] botones) => Abrir<int>(cerrar =>
    {
        var pila = new StackPanel { Spacing = 12 };
        pila.Children.Add(Ui.Titulo(titulo));
        if (texto.Length > 0) pila.Children.Add(Ui.Parrafo(texto));
        var fila = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        for (int i = botones.Length - 1; i >= 0; i--)
        {
            int k = i;
            var b = Ui.Boton(botones[i], () => cerrar(k), principal: i == 0);
            b.Margin = new Thickness(8, 4, 0, 4);
            fila.Children.Add(b);
        }
        pila.Children.Add(fila);
        return Tarjeta(new ScrollViewer { Content = pila, MaxHeight = 640 }, false);
    }, -1, false);

    public Task Avisar(string titulo, string texto) => Preguntar(titulo, texto, "Aceptar");

    /// <summary>Un menú que sube desde abajo. Devuelve la opción elegida, o null.</summary>
    public Task<Opcion?> Menu(string? titulo, IReadOnlyList<Opcion> opciones) => Abrir<Opcion?>(cerrar =>
    {
        var pila = new StackPanel();
        if (titulo != null)
            pila.Children.Add(new TextBlock
            {
                Text = titulo, FontSize = Ui.L(14), Foreground = Ui.Suave, Margin = new Thickness(20, 6, 20, 8), TextWrapping = TextWrapping.Wrap,
            });
        foreach (var o in opciones)
        {
            if (o.Separador) { pila.Children.Add(new Border { Height = 1, Background = Ui.Linea, Margin = new Thickness(0, 6) }); continue; }
            // icono y textos en una rejilla (no en una fila sin límite de ancho), para que los textos largos pasen a otra línea
            var fila = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            if (o.Icono != null)
            {
                var ic = Ui.Icono(o.Icono, 22, o.Marcada ? Ui.Oro : o.Peligro ? Ui.Rojo : Ui.Texto);
                ic.Margin = new Thickness(0, 0, 18, 0); ic.VerticalAlignment = VerticalAlignment.Center;
                fila.Children.Add(ic);
            }
            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(textos, 1);
            textos.Children.Add(new TextBlock { Text = o.Texto, FontSize = Ui.L(16), Foreground = o.Peligro ? Ui.Rojo : o.Marcada ? Ui.Oro : Ui.Texto, TextWrapping = TextWrapping.Wrap });
            if (o.Detalle != null) textos.Children.Add(new TextBlock { Text = o.Detalle, FontSize = Ui.L(13), Foreground = Ui.Suave, TextWrapping = TextWrapping.Wrap });
            fila.Children.Add(textos);
            var b = new Button { Content = fila, Classes = { "opcion" }, IsEnabled = o.Activa, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            b.Click += (_, _) => cerrar(o);
            pila.Children.Add(b);
        }
        return Tarjeta(new ScrollViewer { Content = pila, MaxHeight = 680 }, true);
    }, null, true);

    /// <summary>Pide un texto (p. ej. el nombre de un árbol). Devuelve null si se cancela.</summary>
    public Task<string?> PedirTexto(string titulo, string inicial, string aceptar = "Aceptar", string? marca = null) => Abrir<string?>(cerrar =>
    {
        var caja = new TextBox { Text = inicial, PlaceholderText = marca };
        var pila = new StackPanel { Spacing = 14, MinWidth = 280 };
        pila.Children.Add(Ui.Titulo(titulo));
        pila.Children.Add(caja);
        var fila = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        fila.Children.Add(Ui.Boton("Cancelar", () => cerrar(null)));
        fila.Children.Add(Ui.Boton(aceptar, () => cerrar(caja.Text?.Trim() ?? ""), principal: true));
        pila.Children.Add(fila);
        caja.KeyDown += (_, e) => { if (e.Key == Key.Enter) cerrar(caja.Text?.Trim() ?? ""); };
        caja.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => { caja.Focus(); caja.SelectAll(); }, DispatcherPriority.Input);
        return Tarjeta(pila, false);
    }, null, false);

    /// <summary>
    /// Una hoja a pantalla completa (el editor de una persona, la fusión…). Se cierra llamando a la acción que recibe
    /// <paramref name="crear"/>; con «Atrás» se cierra con el valor <paramref name="cancelado"/>.
    /// </summary>
    public Task<T> Hoja<T>(Func<Action<T>, Control> crear, T cancelado) => Abrir<T>(cerrar =>
    {
        var v = crear(cerrar);
        return new Border { Background = Ui.Fondo, Child = v };
    }, cancelado, false, cerrarAlTocarFuera: false, completa: true);

    /// <summary>Un aviso breve abajo de la pantalla, que se va solo.</summary>
    public void Aviso(string texto, double segundos = 2.8)
    {
        _avisoTexto.Text = texto;
        _aviso.IsVisible = true;
        _avisoTimer?.Stop();
        _avisoTimer = new DispatcherTimer(TimeSpan.FromSeconds(segundos), DispatcherPriority.Normal, (_, _) => { _aviso.IsVisible = false; _avisoTimer?.Stop(); });
        _avisoTimer.Start();
    }

    private void CerrarTodo()
    {
        foreach (var a in _abiertos.ToList()) a.Cancelar();
    }

    /// <summary>Muestra un error sin cerrar la aplicación.</summary>
    public Task Error(string que, Exception ex) => Avisar(que, ex is InvalidDataException or System.Text.Json.JsonException
        ? "El archivo no es un árbol genealógico o está dañado."
        : ex.Message);

    public static string Personas(int n) => n == 1 ? "1 persona" : $"{n} personas";

    public static bool EsArbol(string nombre) => ArbolJson.EsArchivoDeArbol(nombre);
}

/// <summary>Una opción de un menú.</summary>
public sealed record Opcion(string Texto, string? Icono = null, string? Detalle = null, bool Peligro = false, bool Activa = true, bool Marcada = false)
{
    public bool Separador { get; init; }
    public static Opcion Linea => new("") { Separador = true };
}
