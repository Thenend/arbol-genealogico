using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using Microsoft.Win32;

namespace ArbolGenealogico.App;

public partial class EditorPersona : Window
{
    private readonly Persona _persona;
    private readonly string? _rutaJson;
    private string? _foto;
    private string? _enlace;
    private readonly bool _puedeCrearArbol;
    private readonly CampoFecha _nacimiento, _defuncion;

    /// <summary>El usuario ha pedido eliminar a la persona.</summary>
    public bool EliminarSolicitado { get; private set; }

    /// <summary>El usuario ha pedido crear el árbol de esta persona (los cambios del diálogo se aplican antes).</summary>
    public bool CrearArbolSolicitado { get; private set; }

    /// <param name="esPrincipal">Es la persona principal del árbol: su árbol es este mismo, así que no hay opciones de archivo ni se puede eliminar.</param>
    public EditorPersona(Persona persona, string? rutaJson, bool esNueva, bool esPrincipal)
    {
        InitializeComponent();
        Dwm.Aplicar(this);
        _persona = persona; _rutaJson = rutaJson;
        Title = esNueva ? "Nueva persona" : "Editar persona";
        NombreBox.Text = persona.Nombre;
        ApellidosBox.Text = persona.Apellidos;
        HistoriaBox.Text = persona.Historia;
        SexoH.IsChecked = persona.Sexo == Sexo.Hombre;
        SexoM.IsChecked = persona.Sexo == Sexo.Mujer;
        SexoU.IsChecked = persona.Sexo == Sexo.Desconocido;
        // Solo el botón marcado es parada del tabulador: Tab entra y sale del grupo, y las flechas cambian la opción.
        foreach (var o in new[] { SexoH, SexoM, SexoU })
        {
            o.IsTabStop = o.IsChecked == true;
            o.Checked += (_, _) => { foreach (var r in new[] { SexoH, SexoM, SexoU }) r.IsTabStop = r.IsChecked == true; };
        }
        _foto = persona.Foto;
        _enlace = esPrincipal ? null : persona.ArbolEnlazado;
        _puedeCrearArbol = !esPrincipal && !esNueva;
        SeccionArbol.Visibility = esPrincipal ? Visibility.Collapsed : Visibility.Visible;
        EliminarBtn.Visibility = !esPrincipal && !esNueva ? Visibility.Visible : Visibility.Collapsed;
        // las fechas: la defunción solo se ve (y solo cuenta) con «Ha fallecido» marcada
        _nacimiento = new CampoFecha(persona.Nacimiento, 3);
        _defuncion = new CampoFecha(persona.Defuncion, 10);
        NacimientoSitio.Content = _nacimiento.Todo;
        DefuncionSitio.Content = _defuncion.Todo;
        LugarBox.Text = persona.LugarNacimiento ?? "";
        FallecidoBox.IsChecked = persona.HaFallecido;
        MostrarDefuncion();
        FallecidoBox.Checked += (_, _) => MostrarDefuncion();
        FallecidoBox.Unchecked += (_, _) => MostrarDefuncion();
        MostrarFoto(); MostrarEnlace();
        Loaded += (_, _) => { NombreBox.Focus(); NombreBox.SelectAll(); };
        NombreBox.TextChanged += (_, _) => MostrarFoto();
        ApellidosBox.TextChanged += (_, _) => MostrarFoto();
    }

    private void MostrarDefuncion() =>
        _defuncion.Todo.Visibility = FallecidoBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void MostrarFoto()
    {
        var img = Fotos.Cargar(_foto);
        if (img != null)
        {
            FotoCirculo.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            FotoIniciales.Text = "";
        }
        else
        {
            FotoCirculo.Fill = new SolidColorBrush(Color.FromRgb(0x2B, 0x30, 0x40));
            FotoIniciales.Text = Fotos.Iniciales(new Persona { Nombre = NombreBox.Text, Apellidos = ApellidosBox.Text });
        }
        QuitarFotoBtn.IsEnabled = _foto != null;
    }

    private void PonerFoto(Func<string?> origen)
    {
        try
        {
            var foto = origen();
            if (foto == null) return;
            _foto = foto; MostrarFoto();
        }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo cargar la foto", ex.Message); }
    }

    private void Ventana_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = Fotos.DatosTienenImagen(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Ventana_Drop(object sender, DragEventArgs e)
    {
        if (Fotos.DatosTienenImagen(e.Data)) PonerFoto(() => Fotos.DesdeDatos(e.Data));
        e.Handled = true;
    }

    /// <summary>
    /// Ctrl+V pega una imagen como foto, salvo que se esté escribiendo y haya texto en el portapapeles.
    /// ↑/↓ pasan al campo anterior/siguiente (en la historia, solo desde su primera/última línea) hasta el botón Guardar;
    /// ←/→ cambian entre Cancelar y Guardar.
    /// </summary>
    private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && Navegar(e.Key)) { e.Handled = true; return; }
        if (e.Key != Key.V || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        bool escribiendo = Keyboard.FocusedElement is System.Windows.Controls.TextBox && Clipboard.ContainsText();
        if (escribiendo || !Fotos.PortapapelesTieneImagen()) return;
        PonerFoto(Fotos.DesdePortapapeles);
        e.Handled = true;
    }

    private bool Navegar(Key tecla)
    {
        var foco = Keyboard.FocusedElement;
        if (tecla is Key.Left or Key.Right && (foco == GuardarBtn || foco == CancelarBtn))
        {
            (tecla == Key.Left ? CancelarBtn : GuardarBtn).Focus();
            return true;
        }
        if (tecla is not (Key.Up or Key.Down)) return false;
        int paso = tecla == Key.Down ? 1 : -1;
        if (foco == HistoriaBox)
        {
            // Dentro de la historia las flechas mueven el cursor; solo se sale desde la primera o la última línea.
            int linea = HistoriaBox.GetLineIndexFromCharacterIndex(HistoriaBox.CaretIndex);
            int ultima = HistoriaBox.GetLineIndexFromCharacterIndex(HistoriaBox.Text.Length);
            if (paso < 0 ? linea > 0 : linea < ultima) return false;
        }
        var opcionesSexo = new[] { SexoH, SexoM, SexoU };
        var sexo = opcionesSexo.FirstOrDefault(o => o.IsChecked == true) ?? SexoU;
        var orden = new UIElement[] { NombreBox, ApellidosBox, sexo, _nacimiento.Dia, LugarBox, HistoriaBox, GuardarBtn };
        // en los cuadros de las fechas, ↑/↓ van al sexo o a la historia (en la lista del mes cambian el mes)
        var cuadrosFecha = new[] { _nacimiento.Dia, _nacimiento.Anio, _defuncion.Dia, _defuncion.Anio };
        int i = foco == CancelarBtn ? orden.Length - 1
              : opcionesSexo.Any(o => o == foco) ? 2
              : cuadrosFecha.Any(c => c == foco) ? 3
              : Array.IndexOf(orden, foco);
        if (i < 0) return false;
        orden[Math.Clamp(i + paso, 0, orden.Length - 1)].Focus();
        return true;
    }

    /// <summary>Con el foco en el sexo, ← y → cambian entre Hombre / Mujer / Sin especificar.</summary>
    private void Sexo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        int paso = e.Key is Key.Right ? 1 : e.Key is Key.Left ? -1 : 0;
        if (paso == 0) return;
        var opciones = new[] { SexoH, SexoM, SexoU };
        int actual = Array.FindIndex(opciones, o => o.IsKeyboardFocused);
        if (actual < 0) actual = Array.FindIndex(opciones, o => o.IsChecked == true);
        var destino = opciones[Math.Clamp(actual + paso, 0, opciones.Length - 1)];
        destino.IsChecked = true;
        destino.Focus();
        e.Handled = true;
    }

    private void MostrarEnlace()
    {
        EnlaceBox.Text = _enlace ?? "";
        // Crear su árbol solo tiene sentido si aún no tiene uno enlazado.
        CrearArbolBtn.Visibility = _puedeCrearArbol && string.IsNullOrWhiteSpace(_enlace) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CrearArbol_Click(object sender, RoutedEventArgs e)
    {
        CrearArbolSolicitado = true;
        Guardar_Click(sender, e);
    }

    private void ElegirFoto_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Elegir foto", Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp|Todos los archivos|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        try { _foto = Fotos.Importar(dlg.FileName); MostrarFoto(); }
        catch (Exception ex) { DialogoMensaje.Avisar(this, "No se pudo cargar la foto", ex.Message); }
    }

    private void QuitarFoto_Click(object sender, RoutedEventArgs e) { _foto = null; MostrarFoto(); }

    private void ElegirEnlace_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Árbol de esta persona", Filter = "Árbol genealógico (*.ramo)|*.ramo;*.tree;*.json|Todos|*.*" };
        if (_rutaJson != null) dlg.InitialDirectory = Path.GetDirectoryName(_rutaJson);
        if (dlg.ShowDialog(this) != true) return;
        _enlace = _rutaJson != null ? ArbolJson.RutaRelativa(_rutaJson, dlg.FileName) : dlg.FileName;
        MostrarEnlace();
    }

    private void QuitarEnlace_Click(object sender, RoutedEventArgs e) { _enlace = null; MostrarEnlace(); }

    /// <summary>Las dos fechas, comprobadas: si alguna está mal, dice qué le pasa (sin «Ha fallecido», la defunción no cuenta).</summary>
    private bool LeerFechas(out Fecha? nac, out Fecha? def, out string? error)
    {
        (nac, var e1) = _nacimiento.Leer();
        (def, var e2) = FallecidoBox.IsChecked == true ? _defuncion.Leer() : (null, null);
        var errores = new List<string>();
        if (e1 != null) errores.Add("Nacimiento: " + e1);
        if (e2 != null) errores.Add("Defunción: " + e2);
        if (nac is { } n && def is { } d && Fecha.Comparar(d, n) < 0) errores.Add("La fecha de defunción es anterior a la de nacimiento.");
        error = errores.Count > 0 ? string.Join("\n", errores) : null;
        return error == null;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (!LeerFechas(out var nac, out var def, out var error))
        {
            CrearArbolSolicitado = false;
            DialogoMensaje.Avisar(this, "Revisa las fechas", error!);
            return;
        }
        _persona.Nombre = NombreBox.Text.Trim();
        _persona.Apellidos = ApellidosBox.Text.Trim();
        _persona.Historia = HistoriaBox.Text.Trim();
        _persona.Sexo = SexoH.IsChecked == true ? Sexo.Hombre : SexoM.IsChecked == true ? Sexo.Mujer : Sexo.Desconocido;
        _persona.Foto = _foto;
        _persona.ArbolEnlazado = string.IsNullOrWhiteSpace(_enlace) ? null : _enlace;
        _persona.Nacimiento = nac;
        _persona.LugarNacimiento = string.IsNullOrWhiteSpace(LugarBox.Text) ? null : LugarBox.Text.Trim();
        _persona.Defuncion = def;
        _persona.Fallecido = FallecidoBox.IsChecked == true && def == null;
        DialogResult = true;
    }

    private void Eliminar_Click(object sender, RoutedEventArgs e)
    {
        EliminarSolicitado = true;
        DialogResult = false;
    }

    /// <summary>
    /// Una fecha: día, mes (de una lista) y año (solo el año es obligatorio, y nada si no se sabe), con «Aproximada». Antes
    /// de Cristo, el año en negativo: al lado de «Aproximada» se dice así mientras no lo es, y cuando lo es, cómo se lee.
    /// </summary>
    private sealed class CampoFecha
    {
        public readonly TextBox Dia, Anio;
        public readonly ComboBox Mes;
        public readonly CheckBox Aproximada;
        private readonly TextBlock _antes;
        public readonly StackPanel Todo;

        /// <param name="tab">El orden de su primer cuadro con el tabulador (usa cuatro: día, mes, año y la casilla).</param>
        public CampoFecha(Fecha? inicial, int tab)
        {
            Dia = Cuadro(inicial?.Dia?.ToString() ?? "", "Día", 2);
            Anio = Cuadro(inicial is { } fi ? fi.Anio.ToString(System.Globalization.CultureInfo.InvariantCulture) : "", "Año", 5, conSigno: true);
            Mes = new ComboBox { Tag = "Mes", Margin = new Thickness(6, 0, 6, 0) };
            Mes.Items.Add("—");
            for (int m = 1; m <= 12; m++) { var n = Core.Idiomas.Fechas.Mes(m); Mes.Items.Add(char.ToUpper(n[0]) + n[1..]); }
            Mes.SelectedIndex = inicial?.Mes ?? 0;
            var fila = new Grid();
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            fila.Children.Add(Dia);
            Grid.SetColumn(Mes, 1); fila.Children.Add(Mes);
            Grid.SetColumn(Anio, 2); fila.Children.Add(Anio);
            var casilla = (Style)Application.Current.FindResource("Casilla");
            Aproximada = new CheckBox { Style = casilla, Content = "Aproximada", IsChecked = inicial?.Aproximada == true, ToolTip = "Hacia ese año: en las tarjetas, «~1850»" };
            _antes = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Margin = new Thickness(4, 0, 0, 0) };
            MostrarAntes();
            Anio.TextChanged += (_, _) => MostrarAntes();
            var marcas = new WrapPanel { Margin = new Thickness(-10, 4, 0, 0) };
            marcas.Children.Add(Aproximada); marcas.Children.Add(_antes);
            Todo = new StackPanel();
            Todo.Children.Add(fila); Todo.Children.Add(marcas);
            Control[] orden = { Dia, Mes, Anio, Aproximada };
            for (int i = 0; i < orden.Length; i++) orden[i].TabIndex = tab + i;
        }

        /// <summary>Antes de Cristo: cómo se lee el año («44 a. C.») o, si no es negativo, cómo se escribe.</summary>
        private void MostrarAntes()
        {
            bool ac = Core.Idiomas.Fechas.LeerAnio(Anio.Text) is < 0;
            _antes.Text = ac ? Core.Idiomas.Fechas.Anio(Core.Idiomas.Fechas.LeerAnio(Anio.Text)!.Value) : "Antes de Cristo: el año en negativo (-44)";
            _antes.Foreground = (Brush)Application.Current.FindResource(ac ? "AcentoBrush" : "TextoSuaveBrush");
        }

        /// <summary>
        /// Un cuadro para números, centrado, que muestra lo que va en él mientras está vacío. <paramref name="conSigno"/>: se
        /// puede empezar con «-» (los años de antes de Cristo).
        /// </summary>
        private static TextBox Cuadro(string texto, string indicacion, int cifras, bool conSigno = false)
        {
            var t = new TextBox
            {
                Text = texto, Tag = indicacion, MaxLength = cifras, Padding = new Thickness(4, 8, 4, 8),
                TextAlignment = TextAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            // solo cifras (el espacio no llega como texto: va aparte)
            t.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(c => char.IsAsciiDigit(c)
                || (conSigno && c is '-' or '\u2212' && t.CaretIndex == 0 && !t.Text.StartsWith('-')));
            t.PreviewKeyDown += (_, e) => { if (e.Key == Key.Space) e.Handled = true; };
            return t;
        }

        /// <summary>La fecha (o null si está vacía) o lo que le pasa si no es válida.</summary>
        public (Fecha? Fecha, string? Error) Leer()
        {
            string d = Dia.Text.Trim(), a = Anio.Text.Trim();
            int? m = Mes.SelectedIndex > 0 ? Mes.SelectedIndex : null;
            if (d.Length == 0 && a.Length == 0 && m == null) return (null, null);
            if (a.Length == 0) return (null, "falta el año.");
            if (Core.Idiomas.Fechas.LeerAnio(a) is not { } y) return (null, "el año no es válido.");
            int? dd = null;
            if (d.Length > 0)
            {
                if (!int.TryParse(d, out var x) || x < 1) return (null, "el día no es válido.");
                if (m == null) return (null, "con el día, falta el mes.");
                dd = x;
            }
            var f = new Fecha(y, m, dd, Aproximada.IsChecked == true);
            return f.EsValida ? (f, null) : (null, "ese día no existe en ese mes.");
        }
    }
}
