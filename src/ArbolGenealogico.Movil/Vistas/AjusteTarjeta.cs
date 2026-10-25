using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using ArbolGenealogico.Movil.Dibujo;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// Cómo son las tarjetas, a la vista: arriba, la tarjeta de una persona del árbol tal como queda (dibujada por el mismo
/// lienzo que el árbol, sin líneas); debajo, cinco botones que se encienden y se apagan (foto, apellidos, años, historia y
/// marco), la forma (ancha o alta) y la letra. Tocar la foto, los apellidos, los años o la marca de la historia en la
/// tarjeta también los quita. Lo usan el menú del móvil y el panel «Aspecto» del ordenador.
/// </summary>
public sealed class AjusteTarjeta : StackPanel
{
    private readonly LienzoArbol _muestra = new() { Muestra = true, IsHitTestVisible = false };
    private readonly Func<FormaTarjeta> _forma;
    private readonly Func<bool> _marcaHistoria;
    private readonly Action<FormaTarjeta> _cambiarForma;
    private readonly Action<bool> _cambiarMarcaHistoria;
    private readonly Action<Letra> _cambiarLetra;
    private readonly Border _vista;
    private readonly Panel _botones = new StackPanel { Spacing = 6 };

    /// <param name="ancho">Para el ordenador: el ancho de todo (null: el que haya).</param>
    public AjusteTarjeta(Arbol arbol, string? seleccion, Func<FormaTarjeta> forma, Func<bool> marcaHistoria,
        Action<FormaTarjeta> cambiarForma, Action<bool> cambiarMarcaHistoria, Action<Letra> cambiarLetra, double? ancho = null)
    {
        _forma = forma; _marcaHistoria = marcaHistoria;
        _cambiarForma = cambiarForma; _cambiarMarcaHistoria = cambiarMarcaHistoria; _cambiarLetra = cambiarLetra;
        Spacing = 8;
        if (ancho is { } a) Width = a;

        _muestra.Forma = forma();
        _muestra.MarcaHistoria = marcaHistoria();
        _muestra.Mostrar(Muestra(arbol, seleccion), false);
        _vista = new Border
        { CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = _muestra,
            Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand),
        };
        var vista = _vista;
        vista.Tapped += (_, e) => Tocar(_muestra.ParteEn(e.GetPosition(_muestra)));
        ToolTip.SetTip(vista, T("Toca la foto, los apellidos, los años o la marca de la historia para quitarlos"));
        Children.Add(vista);
        Children.Add(_botones);
        Llenar();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Tema.Cambiado += Repintar;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Tema.Cambiado -= Repintar;
    }

    // la letra elegida se ve en los botones de las letras
    private void Repintar() => Llenar();

    /// <summary>
    /// Un árbol con una sola persona para la muestra: la que más cosas tenga para ver cómo cambian (foto, apellidos, años e
    /// historia); a igualdad, la seleccionada o la principal.
    /// </summary>
    private static Arbol Muestra(Arbol a, string? seleccion)
    {
        static int Puntos(Persona p) =>
            (string.IsNullOrEmpty(p.Foto) ? 0 : 8) + (!string.IsNullOrWhiteSpace(p.Nombre) && !string.IsNullOrWhiteSpace(p.Apellidos) ? 4 : 0)
            + (Core.Idiomas.Fechas.Anios(p).Length > 0 ? 2 : 0) + (string.IsNullOrWhiteSpace(p.Historia) ? 0 : 1);
        var orden = new[] { seleccion, a.RaizId }.Where(id => id != null).Select(id => a.Buscar(id!)).OfType<Persona>()
            .Concat(a.Personas).ToList();
        var p = orden.Count == 0 ? new Persona { Id = "muestra", Nombre = T("Nombre"), Apellidos = T("Apellidos") } : orden.MaxBy(Puntos)!;
        var copia = p.Clonar();
        return new Arbol { Nombre = a.Nombre, RaizId = copia.Id, Personas = { copia } };
    }

    /// <summary>Como tocar la muestra en ese punto (para las pruebas).</summary>
    public void TocarEn(Point p) => Tocar(_muestra.ParteEn(p));

    private void Tocar(ParteTarjeta parte)
    {
        var f = _forma();
        switch (parte)
        {
            case ParteTarjeta.Foto: Forma(f with { Foto = false }); break;
            case ParteTarjeta.Apellidos: Forma(f with { SinApellidos = true }); break;
            case ParteTarjeta.Fechas: Forma(f with { SinFechas = true }); break;
            case ParteTarjeta.Historia: Historia(false); break;
        }
    }

    private void Forma(FormaTarjeta f)
    {
        _cambiarForma(f);
        _muestra.Forma = f;
        Llenar();
    }

    private void Historia(bool marcar)
    {
        _cambiarMarcaHistoria(marcar);
        _muestra.MarcaHistoria = marcar;
        Llenar();
    }

    private void Llenar()
    {
        var f = _forma();
        bool historia = _marcaHistoria();
        // de alto, lo que ocupa la tarjeta (la más alta, la vertical con foto, mide 184) y un poco de aire
        _vista.Height = f.Vertical ? 214 : 140;
        _botones.Children.Clear();

        // lo que se pone y se quita: encendido, con el borde y el color de lo elegido; apagado, tachado
        var quitar = new UniformGrid { Columns = Ui.LetraGrande ? 3 : 5 };
        void Interruptor(string texto, string icono, string ayuda, bool encendido, Action cambiar)
        {
            var color = encendido ? Ui.Oro : Ui.Suave;
            var dibujo = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            var ic = Ui.Icono(icono, 30, color, 1.6);
            if (!encendido) ic.Opacity = 0.55;
            dibujo.Children.Add(ic);
            if (!encendido) dibujo.Children.Add(Ui.Icono("M4 20 L20 4", 30, Ui.Suave, 1.8));
            var dentro = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    dibujo,
                    new TextBlock
                    {
                        Text = texto, FontSize = Ui.L(12.5), Foreground = encendido ? Ui.Oro : Ui.Texto, TextAlignment = TextAlignment.Center,
                        FontWeight = encendido ? FontWeight.SemiBold : FontWeight.Normal, TextTrimming = TextTrimming.CharacterEllipsis,
                    },
                },
            };
            var b = new Button
            {
                Content = dentro, Classes = { "mosaico" }, Padding = new Thickness(2, 8, 2, 6), Margin = new Thickness(3),
                BorderBrush = encendido ? Ui.Oro : Brushes.Transparent, BorderThickness = new Thickness(2),
            };
            if (encendido) b.Background = Ui.Panel;
            ToolTip.SetTip(b, ayuda);
            Avalonia.Automation.AutomationProperties.SetName(b, $"{texto}: {(encendido ? T("Con") : T("Sin"))}");
            b.Click += (_, _) => cambiar();
            quitar.Children.Add(b);
        }
        Interruptor(T("Foto"), Ui.Iconos.Camara, f.Foto ? T("Quitar las fotos") : T("Poner las fotos"), f.Foto, () => Forma(f with { Foto = !f.Foto }));
        Interruptor(T("Apellidos"), Ui.Iconos.ConApellidos, f.SinApellidos ? T("Poner los apellidos") : T("Quitar los apellidos"), !f.SinApellidos,
            () => Forma(f with { SinApellidos = !f.SinApellidos }));
        Interruptor(T("Años"), Ui.Iconos.ConFechas, f.SinFechas ? T("Poner los años de nacimiento y defunción") : T("Quitar los años de nacimiento y defunción"),
            !f.SinFechas, () => Forma(f with { SinFechas = !f.SinFechas }));
        Interruptor(T("Historia"), Ui.Iconos.ConHistoria, historia ? T("Sin marcar a quien tiene historia") : T("Marcar a quien tiene historia"), historia,
            () => Historia(!historia));
        Interruptor(T("Marco"), Ui.Iconos.ConTarjeta, f.SinTarjeta ? T("Con tarjeta") : T("Sin tarjeta: solo el nombre (y la foto)"), !f.SinTarjeta,
            () => Forma(f with { SinTarjeta = !f.SinTarjeta }));
        _botones.Children.Add(quitar);

        // la forma: ancha o alta, como dos mitades de un mismo botón
        var forma = new UniformGrid { Columns = 2, Margin = new Thickness(3, 0) };
        void Lado(string texto, string icono, string ayuda, bool marcada, Action elegir)
        {
            var color = marcada ? Ui.SobreOro : Ui.Texto;
            var dentro = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            dentro.Children.Add(Ui.Icono(icono, 24, color, 1.6));
            dentro.Children.Add(new TextBlock
            {
                Text = texto, FontSize = Ui.L(14), Foreground = color, VerticalAlignment = VerticalAlignment.Center,
                FontWeight = marcada ? FontWeight.SemiBold : FontWeight.Normal,
            });
            var b = new Button { Content = dentro, Classes = { "segmento" }, Background = marcada ? Ui.Oro : Brushes.Transparent };
            ToolTip.SetTip(b, ayuda);
            b.Click += (_, _) => { if (!marcada) elegir(); };
            forma.Children.Add(b);
        }
        Lado(T("Ancha"), f.Foto ? Ui.Iconos.TarjetaHorizontalFoto : Ui.Iconos.TarjetaHorizontal, T("Tarjetas anchas"), !f.Vertical, () => Forma(f with { Vertical = false }));
        Lado(T("Alta"), f.Foto ? Ui.Iconos.TarjetaVerticalFoto : Ui.Iconos.TarjetaVertical, T("Tarjetas estrechas"), f.Vertical, () => Forma(f with { Vertical = true }));
        _botones.Children.Add(new Border
        {
            Child = forma, Background = Ui.Secundario, CornerRadius = new CornerRadius(12), Padding = new Thickness(2), Margin = new Thickness(3, 0),
        });

        // la letra: «Aa» con cada una (en la tarjeta de arriba se ve cómo queda)
        var letras = new UniformGrid { Columns = Ui.LetraGrande ? 3 : 6 };
        foreach (var l in Letra.Todas)
        {
            bool marcada = l == Letra.Actual;
            var b = new Button
            {
                Classes = { "mosaico" }, Padding = new Thickness(0, 4), Margin = new Thickness(3), MinHeight = 44 * Ui.EscalaIcono,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = marcada ? Ui.Oro : Brushes.Transparent, BorderThickness = new Thickness(2),
                Content = new TextBlock
                {
                    Text = "Aa", FontFamily = l.Familia, FontWeight = l.PesoNombre, FontSize = 22 * Ui.EscalaIcono * l.Escala,
                    Foreground = marcada ? Ui.Oro : Ui.Texto, HorizontalAlignment = HorizontalAlignment.Center,
                },
            };
            if (marcada) b.Background = Ui.Panel;
            ToolTip.SetTip(b, $"{l.Nombre}: {l.Detalle}");
            Avalonia.Automation.AutomationProperties.SetName(b, l.Nombre);
            b.Click += (_, _) => { if (!marcada) _cambiarLetra(l); };
            letras.Children.Add(b);
        }
        _botones.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children = { new TextBlock { Text = T("Letra"), FontSize = Ui.L(13), Foreground = Ui.Suave, Margin = new Thickness(6, 4, 6, 0) }, letras },
        });
    }
}
