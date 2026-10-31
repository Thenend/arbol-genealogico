namespace ArbolGenealogico.Core.Layout;

/// <summary>
/// Ordenaciones E y F: el árbol tumbado, con las generaciones de izquierda a derecha (los antepasados a la izquierda y los
/// más jóvenes a la derecha) y las parejas una encima de la otra. Se coloca como C (E) o como D (F) con las tarjetas giradas
/// (su alto a lo ancho y su ancho a lo alto) y luego se gira todo. Como las tarjetas son más anchas que altas, las familias
/// que en C van una al lado de la otra ocupan aquí mucho menos: un árbol grande queda más parecido a una hoja.
/// </summary>
internal static class Tumbado
{
    public static LayoutResult Calcular(Model.Arbol arbol, LayoutOptions o)
    {
        // HuecoFilas es aquí el hueco entre columnas (por donde van las líneas hacia los hijos) y HuecoPareja, el que queda
        // entre las dos tarjetas de una pareja, una encima de la otra.
        var girada = new LayoutOptions
        {
            Ordenacion = o.Ordenacion == Ordenacion.F ? Ordenacion.D : Ordenacion.C,
            AnchoCarta = o.AltoCarta, AltoCarta = o.AnchoCarta,
            HuecoPareja = o.HuecoPareja, HuecoHermanos = o.HuecoHermanos, HuecoFamilias = o.HuecoFamilias,
            HuecoFilas = o.HuecoFilas, Margen = o.Margen,
            PesoLineaDirecta = o.PesoLineaDirecta, FactorPadreSobreHijos = o.FactorPadreSobreHijos, MaxBarridos = o.MaxBarridos,
        };
        var r = OrdenacionB.Calcular(arbol, girada);
        var res = new LayoutResult { Opciones = o, Ancho = r.Alto, Alto = r.Ancho };
        foreach (var c in r.Cartas.Values)
            res.Cartas[c.Id] = new CartaPos
            {
                Id = c.Id, X = c.Y + o.AnchoCarta / 2, Y = c.X - o.AltoCarta / 2, Generacion = c.Generacion,
                Cluster = c.Cluster, IndiceEnCluster = c.IndiceEnCluster, Directa = c.Directa, Politica = c.Politica,
            };
        foreach (var cx in r.Conexiones)
        {
            for (int i = 0; i < cx.Puntos.Count; i++) cx.Puntos[i] = new Pt(cx.Puntos[i].Y, cx.Puntos[i].X);
            if (cx.Nudo is { } n) cx.Nudo = new Pt(n.Y, n.X);
            res.Conexiones.Add(cx);
        }
        return res;
    }

    /// <summary>Si la ordenación pone las generaciones de izquierda a derecha.</summary>
    public static bool Es(Ordenacion o) => o is Ordenacion.E or Ordenacion.F;
}
