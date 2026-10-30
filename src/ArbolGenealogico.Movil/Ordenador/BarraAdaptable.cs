using Avalonia;
using Avalonia.Controls;

namespace ArbolGenealogico.Movil.Ordenador;

/// <summary>
/// La fila de la barra de herramientas: unos controles a la izquierda y otros a la derecha. Si no caben todos (una ventana
/// estrecha o un idioma de palabras largas), primero se encogen los elásticos (el nombre del árbol y la búsqueda) hasta su
/// mínimo y luego los botones menos usados pasan al botón «Más», en vez de cortarse sus nombres. Todo se decide al medir,
/// así que no hay vueltas ni medidas viejas: los nombres de los botones se ven siempre enteros.
/// </summary>
internal sealed class BarraAdaptable : Panel
{
    /// <summary>Cuándo se esconde en «Más»: 0, nunca; antes los de menor valor (los del mismo valor van juntos, como un separador y su botón).</summary>
    public static readonly AttachedProperty<int> PrioridadProperty = AvaloniaProperty.RegisterAttached<BarraAdaptable, Control, int>("Prioridad");
    /// <summary>Va en el grupo de la derecha.</summary>
    public static readonly AttachedProperty<bool> DerechaProperty = AvaloniaProperty.RegisterAttached<BarraAdaptable, Control, bool>("Derecha");
    /// <summary>Elástico: hasta dónde se puede encoger (0, nada).</summary>
    public static readonly AttachedProperty<double> MinimoProperty = AvaloniaProperty.RegisterAttached<BarraAdaptable, Control, double>("Minimo");
    /// <summary>Elástico: el ancho que quiere tener si cabe (0, el que pida al medirlo).</summary>
    public static readonly AttachedProperty<double> PreferidoProperty = AvaloniaProperty.RegisterAttached<BarraAdaptable, Control, double>("Preferido");

    static BarraAdaptable()
    {
        AffectsParentMeasure<BarraAdaptable>(PrioridadProperty, DerechaProperty, MinimoProperty, PreferidoProperty);
    }

    public static void Poner(Control c, int prioridad = 0, bool derecha = false, double minimo = 0, double preferido = 0)
    {
        c.SetValue(PrioridadProperty, prioridad);
        c.SetValue(DerechaProperty, derecha);
        c.SetValue(MinimoProperty, minimo);
        c.SetValue(PreferidoProperty, preferido);
    }

    /// <summary>El botón «Más» (uno de los hijos): solo se ve cuando hay algo escondido.</summary>
    public Control? Mas { get; set; }

    /// <summary>Los controles que no caben ahora, en su orden de la barra.</summary>
    public IReadOnlyList<Control> Escondidos => _escondidos.OrderBy(Children.IndexOf).ToList();

    private readonly List<Control> _escondidos = new();
    private readonly Dictionary<Control, double> _anchos = new();

    public BarraAdaptable() => ClipToBounds = true;

    private double Ancho(Control c) => _anchos.TryGetValue(c, out var a) ? a : c.DesiredSize.Width;

    protected override Size MeasureOverride(Size disponible)
    {
        _escondidos.Clear();
        _anchos.Clear();
        var cuentan = new List<Control>();
        foreach (var c in Children)
        {
            if (!c.IsVisible) continue;
            c.Measure(Size.Infinity);
            if (c == Mas) continue;
            cuentan.Add(c);
            if (c.GetValue(PreferidoProperty) is > 0 and var p) _anchos[c] = p;
        }
        double sobra = cuentan.Sum(Ancho) - disponible.Width;

        // primero se encogen los elásticos, cada uno hasta su mínimo
        foreach (var c in cuentan)
        {
            if (sobra <= 0) break;
            double minimo = c.GetValue(MinimoProperty);
            if (minimo <= 0 || Ancho(c) <= minimo) continue;
            double quitar = Math.Min(sobra, Ancho(c) - minimo);
            _anchos[c] = Ancho(c) - quitar;
            sobra -= quitar;
        }
        // y luego los botones van al menú «Más», los menos usados antes
        if (sobra > 0 && Mas is { IsVisible: true } mas)
        {
            sobra += mas.DesiredSize.Width;
            foreach (var nivel in cuentan.Select(c => c.GetValue(PrioridadProperty)).Where(p => p > 0).Distinct().Order())
            {
                if (sobra <= 0) break;
                foreach (var c in cuentan.Where(c => c.GetValue(PrioridadProperty) == nivel)) { _escondidos.Add(c); sobra -= Ancho(c); }
            }
        }

        double alto = 0, ancho = 0;
        foreach (var c in cuentan)
        {
            if (_escondidos.Contains(c)) continue;
            if (_anchos.TryGetValue(c, out var a)) c.Measure(new Size(a, double.PositiveInfinity));
            alto = Math.Max(alto, c.DesiredSize.Height);
            ancho += Ancho(c);
        }
        if (_escondidos.Count > 0) { alto = Math.Max(alto, Mas!.DesiredSize.Height); ancho += Mas.DesiredSize.Width; }
        return new Size(Math.Min(ancho, disponible.Width), alto);
    }

    protected override Size ArrangeOverride(Size tam)
    {
        var fuera = new Rect(-100000, 0, 0, tam.Height);
        bool Visible(Control c) => c.IsVisible && !_escondidos.Contains(c) && (c != Mas || _escondidos.Count > 0);
        double x = 0;
        foreach (var c in Children)
        {
            if (c.GetValue(DerechaProperty)) continue;
            if (!Visible(c)) { c.Arrange(fuera.WithWidth(c.DesiredSize.Width)); continue; }
            double a = c == Mas ? c.DesiredSize.Width : Ancho(c);
            c.Arrange(new Rect(x, 0, a, tam.Height));
            x += a;
        }
        x = tam.Width;
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var c = Children[i];
            if (!c.GetValue(DerechaProperty)) continue;
            if (!Visible(c)) { c.Arrange(fuera.WithWidth(c.DesiredSize.Width)); continue; }
            double a = c == Mas ? c.DesiredSize.Width : Ancho(c);
            x -= a;
            c.Arrange(new Rect(x, 0, a, tam.Height));
        }
        return tam;
    }
}
