using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ArbolGenealogico.App;

public sealed record ItemMenu(string Icono, string Texto, Action? Accion, bool Habilitado = true, string? Ayuda = null, bool Peligro = false, bool Separador = false)
{
    public static ItemMenu Sep() => new("", "", null, Separador: true);
}

/// <summary>Menú contextual oscuro hecho a mano (los menús estándar de WPF no siguen el tema).</summary>
public static class MenuFlotante
{
    private static readonly FontFamily Iconos = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static void Mostrar(FrameworkElement ancla, IEnumerable<ItemMenu> items, PlacementMode placement = PlacementMode.Bottom)
    {
        var popup = new Popup
        {
            PlacementTarget = ancla, Placement = placement, StaysOpen = false,
            AllowsTransparency = true, PopupAnimation = PopupAnimation.Fade, VerticalOffset = 6,
        };
        var panel = new StackPanel { MinWidth = 250 };
        foreach (var it in items)
        {
            if (it.Separador)
            {
                panel.Children.Add(new Border { Height = 1, Background = (Brush)Application.Current.FindResource("BordeBrush"), Margin = new Thickness(10, 5, 10, 5) });
                continue;
            }
            var fila = new Grid();
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            fila.ColumnDefinitions.Add(new ColumnDefinition());
            var color = it.Peligro ? (Brush)Application.Current.FindResource("PeligroBrush") : (Brush)Application.Current.FindResource("TextoBrush");
            fila.Children.Add(new TextBlock { Text = it.Icono, FontFamily = Iconos, FontSize = 14, Foreground = it.Peligro ? color : (Brush)Application.Current.FindResource("AcentoBrush"), VerticalAlignment = VerticalAlignment.Center });
            var texto = new TextBlock { Text = it.Texto, Foreground = color, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(texto, 1); fila.Children.Add(texto);

            var boton = new Button { Content = fila, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12, 8, 12, 8), IsEnabled = it.Habilitado, Margin = new Thickness(0, 1, 0, 1) };
            if (it.Ayuda != null) { boton.ToolTip = it.Ayuda; ToolTipService.SetShowOnDisabled(boton, true); }
            var accion = it.Accion;
            boton.Click += (_, _) => { popup.IsOpen = false; accion?.Invoke(); };
            panel.Children.Add(boton);
        }
        popup.Child = new Border
        {
            Background = (Brush)Application.Current.FindResource("PanelBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("BordeBrush"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(6),
            Margin = new Thickness(12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.5, Color = Colors.Black },
            Child = panel,
        };
        popup.IsOpen = true;
    }
}

/// <summary>Diálogo de mensaje oscuro con botones personalizados. Devuelve el índice pulsado (-1 si se cierra).</summary>
public sealed class DialogoMensaje : Window
{
    public int Resultado { get; private set; } = -1;

    public DialogoMensaje(string titulo, string mensaje, string[] botones, int principal = 0, int cancelar = -1, bool peligro = false)
    {
        Title = titulo; Width = 440; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        Background = (Brush)Application.Current.FindResource("FondoBrush");
        FontFamily = (FontFamily)Application.Current.FindResource("Fuente");
        Dwm.Aplicar(this);

        var raiz = new StackPanel { Margin = new Thickness(26, 24, 26, 20) };
        raiz.Children.Add(new TextBlock { Text = titulo, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = (Brush)Application.Current.FindResource("TextoBrush"), Margin = new Thickness(0, 0, 0, 10) });
        raiz.Children.Add(new TextBlock { Text = mensaje, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, Foreground = (Brush)Application.Current.FindResource("TextoSuaveBrush"), LineHeight = 20 });
        var fila = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
        for (int i = 0; i < botones.Length; i++)
        {
            int idx = i;
            var b = new Button { Content = botones[i], Margin = new Thickness(8, 0, 0, 0), MinWidth = 86 };
            if (i == principal) b.Style = (Style)Application.Current.FindResource(peligro ? "BotonPeligro" : "BotonPrimario");
            b.Click += (_, _) => { Resultado = idx; Close(); };
            fila.Children.Add(b);
        }
        raiz.Children.Add(fila);
        Content = raiz;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Resultado = cancelar; Close(); } };
    }

    public static int Preguntar(Window? dueno, string titulo, string mensaje, string[] botones, int principal = 0, int cancelar = -1, bool peligro = false)
    {
        var d = new DialogoMensaje(titulo, mensaje, botones, principal, cancelar, peligro) { Owner = dueno };
        d.ShowDialog();
        return d.Resultado;
    }

    public static void Avisar(Window? dueno, string titulo, string mensaje) =>
        Preguntar(dueno, titulo, mensaje, new[] { "Aceptar" });
}
