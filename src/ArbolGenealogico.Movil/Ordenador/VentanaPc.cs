using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ArbolGenealogico.Movil.Vistas;

namespace ArbolGenealogico.Movil.Ordenador;

/// <summary>
/// La ventana de Ramo en el ordenador: se abre donde y como estaba, pasa las teclas a la pantalla del árbol (Esc cierra
/// antes lo que haya encima) y, al cerrarla, pregunta si guardar los cambios.
/// </summary>
public sealed class VentanaPc : Window
{
    private bool _cerrando;

    public VentanaPc(Control contenido)
    {
        Title = "Ramo";
        Width = 1320; Height = 820; MinWidth = 960; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = contenido;
        Background = Ui.Fondo;
        try { Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ArbolGenealogico.Movil/Recursos/icono.png"))); }
        catch { }
        Restaurar();
        AddHandler(KeyDownEvent, AlTeclear, RoutingStrategies.Tunnel);
        // los archivos que se arrastran a la ventana (árboles, y fotos sobre las tarjetas)
        DragDrop.SetAllowDrop(this, true);
    }

    private static PantallaPc? Pantalla => Principal.Actual?.Pantalla as PantallaPc;

    private void AlTeclear(object? s, KeyEventArgs e)
    {
        if (Principal.Actual is not { } p) return;
        if (p.HayDialogo)
        {
            if (e.Key == Key.Escape) { p.Escape(); e.Handled = true; }
            return;
        }
        Pantalla?.Teclear(e);
    }

    /// <summary>Donde estaba la última vez, si ese sitio sigue estando en alguna pantalla.</summary>
    private void Restaurar()
    {
        var pref = Preferencias.Actual;
        if (pref.Ventana is not { Length: 4 } v || v[2] < MinWidth || v[3] < MinHeight) return;
        Opened += (_, _) =>
        {
            var r = new PixelRect((int)v[0], (int)v[1], (int)v[2], (int)v[3]);
            if (Screens.All.All(pantalla => !pantalla.WorkingArea.Intersects(r))) return;
            Position = r.Position;
            if (pref.VentanaMaximizada) WindowState = WindowState.Maximized;
        };
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = v[2]; Height = v[3];
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_cerrando || Pantalla is not { } pc) return;
        e.Cancel = true;
        if (!await pc.PuedeCerrar()) return;
        var pref = Preferencias.Actual;
        pref.VentanaMaximizada = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal) pref.Ventana = new double[] { Position.X, Position.Y, Width, Height };
        pref.Guardar();
        _cerrando = true;
        Close();
    }
}
