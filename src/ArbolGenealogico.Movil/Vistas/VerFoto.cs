using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>La foto de una persona en grande, con su nombre debajo. Se cierra tocando en cualquier sitio o con «Atrás».</summary>
public static class VerFoto
{
    /// <summary>Abre la foto si la persona tiene; si no, no hace nada (y devuelve false).</summary>
    public static bool Abrir(Persona p)
    {
        if (Fotos.Imagen(p.Foto) is not { } foto) return false;
        _ = Principal.Actual.Hoja<bool>(cerrar => Crear(p, foto, cerrar), false);
        return true;
    }

    private static Control Crear(Persona p, Bitmap foto, Action<bool> cerrar)
    {
        var raiz = new PanelConAtras { Background = Brushes.Black };
        raiz.AlPedirAtras = () => { cerrar(false); return true; };
        // tocar en cualquier sitio cierra
        raiz.Tapped += (_, e) => { e.Handled = true; cerrar(false); };

        var barra = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), MinHeight = 56 };
        barra.Children.Add(Ui.BotonIcono(Ui.Iconos.Cerrar, T("Cerrar"), () => cerrar(false), Brushes.White));
        DockPanel.SetDock(barra, Dock.Top); raiz.Children.Add(barra);

        var pila = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 56), Spacing = 18 };
        // la foto es cuadrada (la que se guarda): lo más grande que quepa, sin pasar de un tamaño razonable en tabletas
        var imagen = new Image { Source = foto, Stretch = Stretch.Uniform, MaxWidth = 640, MaxHeight = 640 };
        RenderOptions.SetBitmapInterpolationMode(imagen, BitmapInterpolationMode.HighQuality);
        pila.Children.Add(new Border { CornerRadius = new CornerRadius(18), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, Child = imagen });
        var nombre = Ui.Titulo(Nombres.SinNombre(p) ? T("Sin nombre") : p.Nombre, 24);
        nombre.HorizontalAlignment = HorizontalAlignment.Center; nombre.TextAlignment = TextAlignment.Center;
        nombre.TextWrapping = TextWrapping.Wrap;
        pila.Children.Add(nombre);
        if (!string.IsNullOrWhiteSpace(p.Apellidos) && !Nombres.SinNombre(p))
        {
            var apellidos = Ui.Parrafo(p.Apellidos.Trim(), 17, Ui.Suave);
            apellidos.HorizontalAlignment = HorizontalAlignment.Center; apellidos.TextAlignment = TextAlignment.Center;
            apellidos.Margin = new Thickness(0, -12, 0, 0);
            pila.Children.Add(apellidos);
        }
        raiz.Children.Add(pila);
        return raiz;
    }
}
