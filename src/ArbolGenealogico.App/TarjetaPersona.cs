using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.App;

public sealed class Estilo
{
    public required Color Fondo1, Fondo2, Borde, Acento;
    public required double Radio;
    public required string Simbolo;

    public static readonly Estilo Hombre = new()
    {
        Fondo1 = Color.FromRgb(0x24, 0x36, 0x5E), Fondo2 = Color.FromRgb(0x17, 0x22, 0x3F),
        Borde = Color.FromRgb(0x44, 0x6A, 0xBE), Acento = Color.FromRgb(0x72, 0xA2, 0xFF), Radio = 9, Simbolo = "♂",
    };
    public static readonly Estilo Mujer = new()
    {
        Fondo1 = Color.FromRgb(0x50, 0x25, 0x48), Fondo2 = Color.FromRgb(0x34, 0x19, 0x30),
        Borde = Color.FromRgb(0xB0, 0x52, 0x8A), Acento = Color.FromRgb(0xF5, 0x84, 0xC0), Radio = 26, Simbolo = "♀",
    };
    /// <summary>Sexo sin especificar: violeta, a medio camino entre el azul y el rosa (también en la forma).</summary>
    public static readonly Estilo Otro = new()
    {
        Fondo1 = Color.FromRgb(0x38, 0x2C, 0x5C), Fondo2 = Color.FromRgb(0x25, 0x1D, 0x3E),
        Borde = Color.FromRgb(0x7A, 0x5E, 0xC2), Acento = Color.FromRgb(0xB3, 0x9B, 0xFF), Radio = 16, Simbolo = "",
    };

    /// <summary>
    /// Estilo de la tarjeta: por sexo, salvo que la persona no tenga nombre, que se ve en gris
    /// (conserva la forma y el símbolo de su sexo).
    /// </summary>
    public static Estilo De(Sexo s, bool sinNombre)
    {
        var e = s switch { Sexo.Hombre => Hombre, Sexo.Mujer => Mujer, _ => Otro };
        if (!sinNombre) return e;
        return new Estilo
        {
            Fondo1 = Color.FromRgb(0x2D, 0x31, 0x3E), Fondo2 = Color.FromRgb(0x20, 0x23, 0x2D),
            Borde = Color.FromRgb(0x5A, 0x60, 0x72), Acento = Color.FromRgb(0xA3, 0xAA, 0xBE), Radio = e.Radio, Simbolo = e.Simbolo,
        };
    }
}

/// <summary>Tarjeta visual de una persona. Se actualiza con <see cref="Actualizar"/>.</summary>
public sealed class TarjetaPersona : Grid
{
    private static readonly Color Oro = Color.FromRgb(0xF5, 0xC4, 0x51);
    private static readonly FontFamily Iconos = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly FontFamily Texto = new("Segoe UI Variable Text, Segoe UI");

    private readonly Rectangle _halo = new() { IsHitTestVisible = false };
    private readonly Rectangle _sombra = new() { IsHitTestVisible = false };
    private readonly Border _fondo = new();
    private readonly Border _franja = new() { Width = 4, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 20, 0, 20), CornerRadius = new CornerRadius(2) };
    private readonly Rectangle _contorno = new() { IsHitTestVisible = false };
    private readonly Ellipse _fotoCirculo = new() { Width = 54, Height = 54 };
    private readonly Ellipse _fotoAro = new() { Width = 58, Height = 58, StrokeThickness = 2, Fill = Brushes.Transparent };
    private readonly TextBlock _iniciales = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
    private readonly TextBlock _nombre = new() { FontSize = 14.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, FontFamily = Texto };
    private readonly TextBlock _apellidos = new() { FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0xBA, 0xCB)), TextTrimming = TextTrimming.CharacterEllipsis, FontFamily = Texto };
    private readonly TextBlock _simbolo = new() { FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 11, 0), IsHitTestVisible = false };
    private readonly Button _mas = new();
    private readonly Button _enlace = new();
    private readonly TextBlock _historia = new() { Text = "", FontFamily = Iconos, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 40, 9), Foreground = new SolidColorBrush(Color.FromRgb(0xA3, 0xAA, 0xBE)) };

    private bool _seleccionada, _directa, _politica, _destino, _atenuada, _coincidencia;
    private double _opacidadDestino = 1;
    private Estilo _estilo = Estilo.Otro;

    public string PersonaId { get; }
    public event Action<TarjetaPersona>? MasClic;
    public event Action<TarjetaPersona>? EnlaceClic;

    /// <param name="estrecha">Tarjeta estrecha: la foto arriba, centrada, y el nombre y los apellidos debajo.</param>
    public TarjetaPersona(string id, double ancho, double alto, bool estrecha = false)
    {
        PersonaId = id;
        Width = ancho; Height = alto;
        Cursor = System.Windows.Input.Cursors.Hand;
        SnapsToDevicePixels = false;

        _halo.Margin = new Thickness(-7);
        _sombra.Margin = new Thickness(0, 5, 0, -5);
        _sombra.Fill = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
        _contorno.Margin = new Thickness(0.75);

        FrameworkElement contenido;
        if (!estrecha)
        {
            var g = new Grid { Margin = new Thickness(22, 0, 14, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var foto = new Grid { Width = 58, Height = 58, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            foto.Children.Add(_fotoCirculo); foto.Children.Add(_iniciales); foto.Children.Add(_fotoAro);
            Grid.SetColumn(foto, 0); g.Children.Add(foto);
            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            textos.Children.Add(_nombre); textos.Children.Add(_apellidos);
            Grid.SetColumn(textos, 1); g.Children.Add(textos);
            contenido = g;
        }
        else
        {
            // La franja del color del sexo, arriba en horizontal; la foto centrada y los textos debajo, centrados.
            _franja.Width = double.NaN; _franja.Height = 4;
            _franja.HorizontalAlignment = HorizontalAlignment.Stretch; _franja.VerticalAlignment = VerticalAlignment.Top;
            _franja.Margin = new Thickness(30, 9, 30, 0);
            _fotoCirculo.Width = _fotoCirculo.Height = 62; _fotoAro.Width = _fotoAro.Height = 66;
            _iniciales.FontSize = 21;
            var pila = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 22, 8, 0) };
            var foto = new Grid { Width = 66, Height = 66, HorizontalAlignment = HorizontalAlignment.Center };
            foto.Children.Add(_fotoCirculo); foto.Children.Add(_iniciales); foto.Children.Add(_fotoAro);
            pila.Children.Add(foto);
            _nombre.TextAlignment = _apellidos.TextAlignment = TextAlignment.Center;
            _nombre.HorizontalAlignment = _apellidos.HorizontalAlignment = HorizontalAlignment.Center;
            _nombre.Margin = new Thickness(0, 9, 0, 0);
            _nombre.FontSize = 14; _apellidos.FontSize = 12;
            // Los nombres largos pasan a una segunda línea (como mucho) en vez de cortarse.
            _nombre.TextWrapping = TextWrapping.Wrap; _nombre.MaxHeight = 40;
            pila.Children.Add(_nombre); pila.Children.Add(_apellidos);
            contenido = pila;
        }

        ConfigurarBoton(_mas, "", 28, true);
        _mas.HorizontalAlignment = HorizontalAlignment.Center;
        _mas.VerticalAlignment = VerticalAlignment.Bottom;
        _mas.Margin = new Thickness(0, 0, 0, -14);
        _mas.ToolTip = "Añadir familiar";
        _mas.Opacity = 0; _mas.IsHitTestVisible = false;
        _mas.Click += (_, e) => { e.Handled = true; MasClic?.Invoke(this); };

        ConfigurarBoton(_enlace, "", 24, false);
        _enlace.HorizontalAlignment = HorizontalAlignment.Right;
        _enlace.VerticalAlignment = VerticalAlignment.Bottom;
        _enlace.Margin = new Thickness(0, 0, 8, 6);
        _enlace.Visibility = Visibility.Collapsed;
        _enlace.Click += (_, e) => { e.Handled = true; EnlaceClic?.Invoke(this); };

        Children.Add(_halo); Children.Add(_sombra); Children.Add(_fondo); Children.Add(_franja);
        Children.Add(contenido); Children.Add(_simbolo); Children.Add(_historia);
        Children.Add(_contorno); Children.Add(_enlace); Children.Add(_mas);

        MouseEnter += (_, _) => ActualizarBoton();
        MouseLeave += (_, _) => ActualizarBoton();
    }

    private static void ConfigurarBoton(Button b, string glifo, double lado, bool relleno)
    {
        b.Width = lado; b.Height = lado; b.Padding = new Thickness(0);
        b.Cursor = System.Windows.Input.Cursors.Hand;
        b.Focusable = false;
        var circulo = new FrameworkElementFactory(typeof(Border));
        circulo.SetValue(Border.CornerRadiusProperty, new CornerRadius(lado / 2));
        circulo.SetValue(Border.BackgroundProperty, relleno ? new SolidColorBrush(Oro) : new SolidColorBrush(Color.FromArgb(0x30, 0xF5, 0xC4, 0x51)));
        circulo.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(relleno ? (byte)0xFF : (byte)0x80, 0x0E, 0x10, 0x16)));
        circulo.SetValue(Border.BorderThicknessProperty, new Thickness(relleno ? 2 : 0));
        var texto = new FrameworkElementFactory(typeof(TextBlock));
        texto.SetValue(TextBlock.TextProperty, glifo);
        texto.SetValue(TextBlock.FontFamilyProperty, Iconos);
        texto.SetValue(TextBlock.FontSizeProperty, lado * (relleno ? 0.45 : 0.5));
        texto.SetValue(TextBlock.ForegroundProperty, relleno ? new SolidColorBrush(Color.FromRgb(0x1A, 0x14, 0x05)) : new SolidColorBrush(Oro));
        texto.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        texto.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        circulo.AppendChild(texto);
        b.Template = new ControlTemplate(typeof(Button)) { VisualTree = circulo };
    }

    public void Actualizar(Persona p, bool directa, bool politica)
    {
        bool sinNombre = string.IsNullOrWhiteSpace(p.Nombre) && string.IsNullOrWhiteSpace(p.Apellidos);
        _estilo = Estilo.De(p.Sexo, sinNombre);
        _directa = directa; _politica = politica;
        var e = _estilo;

        _fondo.CornerRadius = new CornerRadius(e.Radio);
        _fondo.Background = new LinearGradientBrush(e.Fondo1, e.Fondo2, 90);
        _sombra.RadiusX = _sombra.RadiusY = e.Radio;
        _halo.RadiusX = _halo.RadiusY = e.Radio + 7;
        _contorno.RadiusX = _contorno.RadiusY = Math.Max(0, e.Radio - 0.75);
        _franja.Background = new SolidColorBrush(e.Acento);
        _franja.Opacity = politica ? 0.45 : 0.95;
        _fotoAro.Stroke = new SolidColorBrush(e.Acento);
        _simbolo.Text = e.Simbolo;
        _simbolo.Foreground = new SolidColorBrush(e.Acento);
        _simbolo.Opacity = 0.9;

        var foto = Fotos.Cargar(p.Foto);
        if (foto != null)
        {
            _fotoCirculo.Fill = new ImageBrush(foto) { Stretch = Stretch.UniformToFill };
            _iniciales.Visibility = Visibility.Collapsed;
        }
        else
        {
            _fotoCirculo.Fill = new SolidColorBrush(Color.FromArgb(0x55, e.Acento.R, e.Acento.G, e.Acento.B));
            _iniciales.Text = Fotos.Iniciales(p);
            _iniciales.Visibility = Visibility.Visible;
        }

        _nombre.Text = sinNombre ? "Sin nombre" : (string.IsNullOrWhiteSpace(p.Nombre) ? p.Apellidos.Trim() : p.Nombre.Trim());
        _nombre.FontStyle = sinNombre ? FontStyles.Italic : FontStyles.Normal;
        _nombre.Opacity = sinNombre ? 0.55 : 1;
        bool apellidosAparte = !sinNombre && !string.IsNullOrWhiteSpace(p.Nombre) && !string.IsNullOrWhiteSpace(p.Apellidos);
        _apellidos.Text = apellidosAparte ? p.Apellidos.Trim() : "";
        _apellidos.Visibility = apellidosAparte ? Visibility.Visible : Visibility.Collapsed;

        _historia.Visibility = string.IsNullOrWhiteSpace(p.Historia) ? Visibility.Collapsed : Visibility.Visible;
        _enlace.Visibility = string.IsNullOrEmpty(p.ArbolEnlazado) ? Visibility.Collapsed : Visibility.Visible;
        _enlace.ToolTip = string.IsNullOrEmpty(p.ArbolEnlazado) ? null : "Abrir su árbol: " + p.ArbolEnlazado;
        _historia.Margin = new Thickness(0, 0, _enlace.Visibility == Visibility.Visible ? 40 : 14, 9);
        ToolTip = string.IsNullOrWhiteSpace(p.Historia) ? null : Resumir(p.Historia);

        AplicarOpacidad(false);
        AplicarBorde();
    }

    private static string Resumir(string s)
    {
        s = s.Trim();
        return s.Length <= 320 ? s : s[..320] + "…";
    }

    public bool Seleccionada
    {
        get => _seleccionada;
        set { _seleccionada = value; AplicarBorde(); ActualizarBoton(); }
    }

    /// <summary>La tarjeta coincide con la búsqueda actual: lleva un anillo verde lima (el violeta es el de sexo sin especificar).</summary>
    public bool Coincidencia
    {
        get => _coincidencia;
        set { if (_coincidencia == value) return; _coincidencia = value; AplicarBorde(); }
    }

    /// <summary>Tarjeta fuera de la familia directa de la persona seleccionada: se oscurece.</summary>
    public bool Atenuada
    {
        get => _atenuada;
        set { if (_atenuada == value) return; _atenuada = value; AplicarOpacidad(true); }
    }

    private double OpacidadNormal => _atenuada ? 0.28 : _politica ? 0.93 : 1;

    private void AplicarOpacidad(bool animar)
    {
        double destino = _opacidadDestino = OpacidadNormal;
        if (!animar || !IsLoaded) { BeginAnimation(OpacityProperty, null); Opacity = destino; return; }
        Animar(Opacity, destino, 0);
    }

    /// <summary>Aparece con un fundido (para las tarjetas recién añadidas).</summary>
    public void AparecerSuavemente()
    {
        double destino = _opacidadDestino = OpacidadNormal;
        Opacity = 0;
        Animar(0, destino, 120);
    }

    private void Animar(double desde, double destino, int retrasoMs)
    {
        var a = new System.Windows.Media.Animation.DoubleAnimation(desde, destino, TimeSpan.FromMilliseconds(260))
        {
            BeginTime = TimeSpan.FromMilliseconds(retrasoMs),
        };
        a.Completed += (_, _) =>
        {
            if (Math.Abs(_opacidadDestino - destino) > 1e-9) return;   // otra animación más reciente manda
            BeginAnimation(OpacityProperty, null);
            Opacity = destino;
        };
        BeginAnimation(OpacityProperty, a);
    }

    /// <summary>Se está arrastrando una imagen sobre esta tarjeta: se ilumina para indicar dónde caerá.</summary>
    public bool DestinoDrop
    {
        get => _destino;
        set { if (_destino == value) return; _destino = value; AplicarBorde(); }
    }

    private void ActualizarBoton()
    {
        bool ver = IsMouseOver || _seleccionada;
        _mas.Opacity = ver ? 1 : 0;
        _mas.IsHitTestVisible = ver;
    }

    private void AplicarBorde()
    {
        var e = _estilo;
        Color color; double grosor;
        if (_destino) { color = Color.FromRgb(0x5E, 0xEA, 0xD4); grosor = 3.5; }
        else if (_seleccionada) { color = Colors.White; grosor = 2.5; }
        else if (_coincidencia) { color = Color.FromRgb(0xA3, 0xE6, 0x35); grosor = 3; }
        else if (_directa) { color = Oro; grosor = 2.5; }
        else { color = e.Borde; grosor = 1.5; }
        _contorno.Stroke = new SolidColorBrush(color);
        _contorno.StrokeThickness = grosor;
        _contorno.StrokeDashArray = _politica && !_seleccionada ? new DoubleCollection { 4, 3 } : null;
        _halo.Fill = _destino ? new SolidColorBrush(Color.FromArgb(0x40, 0x5E, 0xEA, 0xD4))
                   : _seleccionada ? new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF))
                   : _coincidencia ? new SolidColorBrush(Color.FromArgb(0x38, 0xA3, 0xE6, 0x35))
                   : _directa ? new SolidColorBrush(Color.FromArgb(0x26, Oro.R, Oro.G, Oro.B)) : null;
    }
}
