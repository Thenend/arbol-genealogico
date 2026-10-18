using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// Antes de fusionar un árbol con otro: qué pasará (personas en común, nuevas, datos que se completan), quién parece la
/// misma persona en los dos (cuando no vienen de una copia del mismo árbol) y qué dato se queda si son distintos.
/// </summary>
public static class PantallaFusion
{
    public static async Task Fusionar(string rutaDestino, Arbol otro, string nombreOtro)
    {
        var mio = ArbolJson.Cargar(rutaDestino, out _);
        var parecidos = Fusion.Parecidos(mio, otro);
        var marcados = parecidos.ToHashSet();
        bool preferirOtro = false;
        Dictionary<string, string> Equivalencias() => marcados.ToDictionary(p => p.IdSuyo, p => p.IdMio);

        // si ya lo tiene todo (por ejemplo, le han devuelto su propio árbol), no hay nada que decidir: se abre el suyo
        if (!Fusion.Probar(mio, otro, Equivalencias()).AportaAlgo)
        {
            if (Principal.Actual.Pantalla is not PantallaArbol { } abierta || !string.Equals(abierta.Ruta, rutaDestino, StringComparison.OrdinalIgnoreCase))
                PantallaInicio.Abrir(rutaDestino);
            Principal.Actual.Aviso(T("«{0}» no trae nada nuevo: «{1}» ya tiene todo lo que hay en él.", nombreOtro, mio.Nombre), 4);
            return;
        }

        bool ok = await Principal.Actual.Hoja<bool>(cerrar =>
        {
            var raiz = new DockPanel();
            var barra = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Background = Ui.Barra, MinHeight = 56 };
            barra.Children.Add(Ui.BotonIcono(Ui.Iconos.Cerrar, T("Cancelar"), () => cerrar(false)));
            var t = Ui.Titulo(T("Fusionar árboles"), 19); t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(t, 1); barra.Children.Add(t);
            DockPanel.SetDock(barra, Dock.Top); raiz.Children.Add(barra);

            var fusionar = Ui.Boton(T("Fusionar"), () => cerrar(true), principal: true, icono: Ui.Iconos.Fusionar);
            fusionar.HorizontalAlignment = HorizontalAlignment.Stretch; fusionar.Margin = new Thickness(16, 8, 16, 16);
            DockPanel.SetDock(fusionar, Dock.Bottom); raiz.Children.Add(fusionar);

            var pila = new StackPanel { Spacing = 12, Margin = new Thickness(20, 16, 20, 20), MaxWidth = 600 };
            raiz.Children.Add(new ScrollViewer { Content = pila });
            pila.Children.Add(Ui.Parrafo(T("Lo que tiene «{0}» se añade a «{1}». Nada se borra: las personas que están en los dos se juntan en una.", nombreOtro, mio.Nombre), 15, Ui.Texto));

            var resumen = new StackPanel { Spacing = 6 };
            pila.Children.Add(new Border { Background = Ui.Panel, CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12), Child = resumen });
            var diferencias = new StackPanel { Spacing = 8 };
            // «¿Son la misma persona?»: solo si hay sugerencias o si algo quedaría suelto (si no, todos están ya emparejados)
            var seccion = new StackPanel { Spacing = 8 };

            void Actualizar()
            {
                var inf = Fusion.Probar(mio, otro, Equivalencias(), preferirOtro);
                resumen.Children.Clear();
                void Linea(string s, IBrush? color = null) => resumen.Children.Add(new TextBlock { Text = s, FontSize = Ui.L(15), TextWrapping = TextWrapping.Wrap, Foreground = color ?? Ui.Texto });
                Linea("• " + N(inf.Comunes, "1 persona en los dos árboles", "{0} personas en los dos árboles"));
                Linea("• " + N(inf.Nuevas, "Se añadirá 1 persona", "Se añadirán {0} personas"));
                if (inf.Completadas > 0) Linea("• " + N(inf.Completadas, "Se completarán datos de 1 persona (fotos, historias…)", "Se completarán datos de {0} personas (fotos, historias…)"));
                if (inf.Nuevas == 0 && inf.Completadas == 0 && inf.ConDiferencias.Count == 0) Linea(T("Ya tienes todo lo que trae: no cambiará nada."), Ui.Suave);
                if (inf.Sueltas > 0)
                    Linea("⚠ " + N(inf.Sueltas,
                        "1 persona no tiene relación con las de tu árbol y no se vería. Indica abajo quién es quién, o ábrelo mejor como árbol aparte.",
                        "{0} personas no tienen relación con las de tu árbol y no se verían. Indica abajo quién es quién, o ábrelo mejor como árbol aparte."), Ui.Oro);
                foreach (var a in inf.Avisos.Distinct()) Linea("⚠ " + a, Ui.Suave);
                diferencias.IsVisible = inf.ConDiferencias.Count > 0;
                if (diferencias.Children.Count > 0 && diferencias.Children[0] is TextBlock tb)
                    tb.Text = N(inf.ConDiferencias.Count, "1 persona con algún dato distinto en los dos árboles. ¿Cuál se queda?",
                        "{0} personas con algún dato distinto en los dos árboles. ¿Cuál se queda?");
                // si todo lo nuevo quedaría suelto (no se vería), no tiene sentido fusionar
                bool util = (inf.Nuevas > inf.Sueltas) || inf.Completadas > 0 || (preferirOtro && inf.ConDiferencias.Count > 0);
                fusionar.IsEnabled = util;
                seccion.IsVisible = parecidos.Count > 0 || inf.Sueltas > 0;
            }

            // quién es quién: las sugerencias (se llaman igual) y las que se elijan a mano
            var pares = new StackPanel { Spacing = 4 };
            seccion.Children.Add(Ui.Titulo(T("¿Son la misma persona?"), 17));
            var explicacion = Ui.Parrafo("", 14);
            seccion.Children.Add(explicacion);
            seccion.Children.Add(pares);
            pila.Children.Add(seccion);
            void PintarPares()
            {
                explicacion.Text = parecidos.Count > 0
                    ? T("Estas personas no vienen del mismo árbol, pero parecen la misma. Las marcadas se juntarán en una.")
                    : T("Si alguien está en los dos árboles (por ejemplo, la persona que une las dos familias), indícalo para que se junten.");
                pares.Children.Clear();
                foreach (var par in parecidos)
                {
                    var pm = mio.Obtener(par.IdMio); var ps = otro.Obtener(par.IdSuyo);
                    var textos = new StackPanel();
                    var titulo = Nombres.Mostrar(pm) == Nombres.Mostrar(ps) ? Nombres.Mostrar(pm) : Nombres.Mostrar(pm) + " = " + Nombres.Mostrar(ps);
                    textos.Children.Add(new TextBlock { Text = titulo, FontSize = Ui.L(16), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
                    textos.Children.Add(new TextBlock { Text = T("En el tuyo: {0}", Contexto(mio, pm)), FontSize = Ui.L(13), Foreground = Ui.Suave, TextWrapping = TextWrapping.Wrap });
                    textos.Children.Add(new TextBlock { Text = T("En el otro: {0}", Contexto(otro, ps)), FontSize = Ui.L(13), Foreground = Ui.Suave, TextWrapping = TextWrapping.Wrap });
                    var check = new CheckBox { IsChecked = marcados.Contains(par), Content = textos, Margin = new Thickness(0, 4) };
                    check.IsCheckedChanged += (_, _) => { if (check.IsChecked == true) marcados.Add(par); else marcados.Remove(par); Actualizar(); };
                    pares.Children.Add(check);
                }
                var mano = Ui.Boton(T("Indicar quién es quién…"), async () =>
                {
                    var p = Principal.Actual;
                    var usadosSuyos = parecidos.Select(x => x.IdSuyo).ToHashSet();
                    var usadosMios = parecidos.Select(x => x.IdMio).ToHashSet();
                    var suyas = otro.Personas.Where(x => !usadosSuyos.Contains(x.Id) && mio.Buscar(x.Id) == null).OrderBy(x => x.NombreCompleto).ToList();
                    var libres = mio.Personas.Where(x => !usadosMios.Contains(x.Id) && otro.Buscar(x.Id) == null).ToList();
                    if (suyas.Count == 0 || libres.Count == 0)
                    {
                        await p.Avisar(T("No queda nadie por emparejar"), T("Todas las personas del árbol recibido ya están reconocidas en el tuyo."));
                        return;
                    }
                    var ops1 = suyas.Select(x => new Opcion(Nombres.Mostrar(x), Ui.Iconos.Persona, Contexto(otro, x))).ToList();
                    var e1 = await p.Menu(T("Del árbol «{0}», ¿quién está también en el tuyo?", nombreOtro), ops1);
                    if (e1 == null) return;
                    var ps = suyas[ops1.IndexOf(e1)];
                    var mias = mio.Personas.Where(x => !usadosMios.Contains(x.Id) && otro.Buscar(x.Id) == null).OrderBy(x => x.NombreCompleto).ToList();
                    var ops2 = mias.Select(x => new Opcion(Nombres.Mostrar(x), Ui.Iconos.Persona, Contexto(mio, x))).ToList();
                    var e2 = await p.Menu(T("¿Quién es {0} en tu árbol?", Nombres.Mostrar(ps)), ops2);
                    if (e2 == null) return;
                    var nuevo = new Parecido(mias[ops2.IndexOf(e2)].Id, ps.Id, 0);
                    parecidos.Add(nuevo); marcados.Add(nuevo);
                    PintarPares(); Actualizar();
                }, icono: Ui.Iconos.Persona);
                mano.Margin = new Thickness(0, 6, 0, 0);
                pares.Children.Add(mano);
            }
            PintarPares();

            diferencias.Children.Add(new TextBlock { FontSize = Ui.L(15), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
            var mios = new RadioButton { Content = T("Los de mi árbol"), IsChecked = true, GroupName = "dif" };
            var suyos = new RadioButton { Content = T("Los del árbol recibido"), GroupName = "dif" };
            mios.IsCheckedChanged += (_, _) => { preferirOtro = suyos.IsChecked == true; Actualizar(); };
            suyos.IsCheckedChanged += (_, _) => { preferirOtro = suyos.IsChecked == true; Actualizar(); };
            diferencias.Children.Add(mios); diferencias.Children.Add(suyos);
            diferencias.Children.Add(Ui.Parrafo(T("Los datos que solo están en uno de los dos se añaden siempre."), 13));
            pila.Children.Add(diferencias);
            Actualizar();
            return raiz;
        }, false);
        if (!ok) return;

        var antes = mio.Clonar();
        var inf = Fusion.Fusionar(mio, otro, Equivalencias(), preferirOtro);
        App.Biblioteca.Guardar(rutaDestino, mio);
        Principal.Actual.Ir(new PantallaArbol(rutaDestino, antes));
        Principal.Actual.Aviso(N(inf.Nuevas, "Fusionado: 1 persona nueva", "Fusionado: {0} personas nuevas") +
                               (inf.Completadas > 0 ? N(inf.Completadas, ", 1 completada", ", {0} completadas") : "") +
                               ". " + T("Se puede deshacer en el menú."), 4);
    }

    /// <summary>Quién es en su árbol: hijo/a de…, pareja de…</summary>
    private static string Contexto(Arbol a, Persona p)
    {
        var partes = new List<string>();
        if (a.UnionComoHijo(p.Id) is { } u && u.Parejas.Count > 0)
        {
            var padres = u.Parejas.Select(x => Nombres.Mostrar(a.Buscar(x))).ToList();
            var quienes = padres.Count == 2 ? T("{0} y {1}", padres[0], padres[1]) : padres[0];
            partes.Add(p.Sexo == Sexo.Mujer ? T("hija de {0}", quienes) : p.Sexo == Sexo.Hombre ? T("hijo de {0}", quienes) : T("hijo/a de {0}", quienes));
        }
        var parejas = a.UnionesComoPareja(p.Id).SelectMany(x => x.Parejas).Where(x => x != p.Id).Select(x => Nombres.Mostrar(a.Buscar(x))).ToList();
        if (parejas.Count > 0) partes.Add(T("pareja de {0}", string.Join(", ", parejas)));
        int hijos = a.UnionesComoPareja(p.Id).Sum(x => x.Hijos.Count);
        if (hijos > 0) partes.Add(N(hijos, "1 hijo/a", "{0} hijos"));
        return partes.Count == 0 ? T("sin familiares") : string.Join("; ", partes);
    }
}
