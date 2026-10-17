using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Presentacion;

/// <summary>
/// Forma de las tarjetas, como en la aplicación de escritorio: horizontal o vertical, con foto o sin ella, con tarjeta o sin
/// ella (solo el nombre) y con apellidos o sin ellos. Los tamaños son los mismos, para que el árbol se coloque igual en
/// todas las versiones.
/// </summary>
public readonly record struct FormaTarjeta(bool Vertical, bool Foto, bool SinTarjeta = false, bool SinApellidos = false)
{
    public static readonly FormaTarjeta Ancha = new(false, true);

    /// <summary>Sin tarjeta y sin apellidos (porque se ocultan o porque nadie los tiene): cada persona ocupa una sola línea.</summary>
    public bool SoloNombre { get; init; }

    public (double Ancho, double Alto) Tamano => (Vertical, Foto, SinTarjeta) switch
    {
        (false, true, true) when SoloNombre => (196, 64),
        (false, false, true) when SoloNombre => (120, 35),
        (true, true, true) when SoloNombre => (138, 134),
        (true, false, true) when SoloNombre => (112, 62),
        (false, true, false) => (244, 88),
        (false, false, false) => (150, 66),
        (true, true, false) => (150, 184),
        (true, false, false) => (124, 118),
        (false, true, true) => (196, 64),
        (false, false, true) => (120, 56),
        (true, true, true) => (138, 156),
        (true, false, true) => (112, 90),
    };

    /// <summary>Tamaño de la letra del nombre (px); la de los apellidos es algo menor.</summary>
    public double LetraNombre => Vertical ? 22 : 23;

    public override string ToString() =>
        $"{(Vertical ? "vertical" : "horizontal")} {(Foto ? "foto" : "sin-foto")}{(SinTarjeta ? " sin-tarjeta" : "")}{(SinApellidos ? " sin-apellidos" : "")}";

    public static FormaTarjeta Leer(string? s)
    {
        var palabras = (s ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length == 0) return Ancha;
        return new(palabras.Contains("vertical"), !palabras.Contains("sin-foto"), palabras.Contains("sin-tarjeta"), palabras.Contains("sin-apellidos"));
    }

    /// <summary>Las opciones de colocación que usa la aplicación de escritorio con esta forma.</summary>
    public LayoutOptions Opciones(Ordenacion ordenacion)
    {
        var d = new LayoutOptions();
        var (w, h) = Tamano;
        return new LayoutOptions
        {
            Ordenacion = ordenacion,
            AnchoCarta = w,
            AltoCarta = h,
            HuecoFilas = SinTarjeta ? 48 : Foto ? d.HuecoFilas : 74,
            HuecoPareja = SinTarjeta ? 22 : Foto ? d.HuecoPareja : 26,
            AlturaEnlace = this is { SinTarjeta: true, Vertical: true, Foto: true } ? 35 : null,
            HuecoHermanos = SinTarjeta ? 16 : d.HuecoHermanos,
            HuecoFamilias = SinTarjeta ? 44 : d.HuecoFamilias,
        };
    }

    /// <summary>La forma con la que se dibuja este árbol (añade <see cref="SoloNombre"/> si toca).</summary>
    public FormaTarjeta Para(Arbol a) => this with
    {
        SoloNombre = SinTarjeta && (SinApellidos || !a.Personas.Any(p => !string.IsNullOrWhiteSpace(p.Nombre) && !string.IsNullOrWhiteSpace(p.Apellidos))),
    };
}
