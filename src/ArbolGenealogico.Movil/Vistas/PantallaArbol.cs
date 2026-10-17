using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Core.Presentacion;
using ArbolGenealogico.Movil.Dibujo;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// Un árbol abierto: el lienzo, la barra de arriba (volver, nombre, buscar, menú) y, abajo, la ficha de la persona
/// seleccionada con sus botones. Cada cambio se guarda al momento y se puede deshacer.
/// </summary>
public sealed class PantallaArbol : Grid, IPantalla
{
    private readonly LienzoArbol _lienzo = new();
    private readonly TextBlock _titulo = new() { FontSize = Ui.L(19), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Grid _barra = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), Background = Ui.Barra, MinHeight = 56 };
    private readonly Grid _barraBusqueda = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), Background = Ui.Barra, MinHeight = 56, IsVisible = false };
    private readonly TextBox _buscar = new() { PlaceholderText = "Buscar por nombre", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6) };
    private readonly TextBlock _contador = new() { Foreground = Ui.Suave, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly Border _ficha = new()
    {
        Background = Ui.Panel, CornerRadius = new CornerRadius(18, 18, 0, 0), Padding = new Thickness(16, 12, 12, 12),
        VerticalAlignment = VerticalAlignment.Bottom, IsVisible = false,
        BorderBrush = Ui.Linea, BorderThickness = new Thickness(0, 1, 0, 0),
    };
    private readonly Button _encuadrar;

    private string _ruta;
    private Arbol _arbol;
    private readonly List<Arbol> _deshacer = new(), _rehacer = new();
    /// <summary>Los árboles de los que se ha venido siguiendo enlaces (para volver con «Atrás»).</summary>
    private readonly Stack<(string Ruta, string? Seleccion)> _pila = new();
    private List<string> _coincidencias = new();
    private int _actual = -1;

    /// <param name="deshacer">El árbol como estaba antes de un cambio hecho fuera (una fusión), para poder deshacerlo.</param>
    public PantallaArbol(string ruta, Arbol? deshacer = null)
    {
        RowDefinitions = new RowDefinitions("Auto,*");
        _ruta = ruta;
        _arbol = Cargar(ruta);
        if (deshacer != null) _deshacer.Add(deshacer);

        // barra de arriba
        var atras = Ui.BotonIcono(Ui.Iconos.Atras, "Volver", () => Atras());
        _barra.Children.Add(atras);
        Grid.SetColumn(_titulo, 1); _barra.Children.Add(_titulo);
        _titulo.PointerPressed += (_, _) => _ = Renombrar();
        var buscar = Ui.BotonIcono(Ui.Iconos.Buscar, "Buscar", AbrirBusqueda);
        Grid.SetColumn(buscar, 2); _barra.Children.Add(buscar);
        var menu = Ui.BotonIcono(Ui.Iconos.Menu, "Más opciones", () => _ = Menu());
        Grid.SetColumn(menu, 3); _barra.Children.Add(menu);
        Children.Add(_barra);

        // barra de búsqueda (sustituye a la de arriba mientras se busca)
        _barraBusqueda.Children.Add(Ui.BotonIcono(Ui.Iconos.Atras, "Cerrar la búsqueda", CerrarBusqueda));
        Grid.SetColumn(_buscar, 1); _barraBusqueda.Children.Add(_buscar);
        Grid.SetColumn(_contador, 2); _barraBusqueda.Children.Add(_contador);
        var ant = Ui.BotonIcono(Ui.Iconos.Anterior, "Anterior", () => IrACoincidencia(-1));
        Grid.SetColumn(ant, 3); _barraBusqueda.Children.Add(ant);
        var sig = Ui.BotonIcono(Ui.Iconos.Siguiente, "Siguiente", () => IrACoincidencia(1));
        Grid.SetColumn(sig, 4); _barraBusqueda.Children.Add(sig);
        _buscar.TextChanged += (_, _) => Buscar();
        _buscar.KeyDown += (_, e) => { if (e.Key == Key.Enter) { IrACoincidencia(1); e.Handled = true; } };
        Children.Add(_barraBusqueda);

        // el árbol, con la ficha y el botón de encuadrar encima
        var zona = new Grid();
        Grid.SetRow(zona, 1);
        zona.Children.Add(_lienzo);
        _encuadrar = new Button
        {
            Content = Ui.Icono(Ui.Iconos.Encuadrar, 22), Classes = { "plano" }, Background = Ui.Panel,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 14, 14),
        };
        ToolTip.SetTip(_encuadrar, "Ver todo el árbol");
        _encuadrar.Click += (_, _) => _lienzo.Encuadrar();
        zona.Children.Add(_encuadrar);
        zona.Children.Add(_ficha);
        Children.Add(zona);

        var pref = Preferencias.Actual;
        _lienzo.Forma = FormaTarjeta.Leer(pref.Forma) with { SinTarjeta = false };
        _lienzo.Ordenacion = Enum.TryParse<Ordenacion>(pref.Ordenacion, out var o) ? o : Ordenacion.C;
        _lienzo.SeleccionCambiada += id =>
        {
            ActualizarFicha();
            // que la ficha que aparece abajo no tape la tarjeta tocada (cuando ya se sabe lo que mide)
            if (id != null)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _lienzo.AsegurarVisible(id, _ficha.IsVisible ? _ficha.Bounds.Height : 0),
                    Avalonia.Threading.DispatcherPriority.Background);
        };
        _lienzo.FichaPedida += id => _ = Editar(id, false);
        _lienzo.MenuPedido += id => _ = AnadirFamiliar(id);
        _lienzo.EnlacePedido += id => _ = AbrirEnlace(id);
        Mostrar(true);
    }

    private static Arbol Cargar(string ruta)
    {
        var a = ArbolJson.Cargar(ruta, out var avisos);
        if (avisos.Count > 0) Principal.Actual.Aviso(string.Join("\n", avisos), 5);
        return a;
    }

    private void Mostrar(bool encuadrar)
    {
        _titulo.Text = string.IsNullOrWhiteSpace(_arbol.Nombre) ? Path.GetFileNameWithoutExtension(_ruta) : _arbol.Nombre;
        _lienzo.Mostrar(_arbol, encuadrar);
        ActualizarFicha();
    }

    public bool Atras()
    {
        if (_barraBusqueda.IsVisible) { CerrarBusqueda(); return true; }
        if (_lienzo.Seleccion != null) { _lienzo.Seleccion = null; return true; }
        if (_pila.Count > 0)
        {
            var (ruta, sel) = _pila.Pop();
            CambiarDeArbol(ruta, sel);
            return true;
        }
        Principal.Actual.Ir(new PantallaInicio());
        return true;
    }

    private void CambiarDeArbol(string ruta, string? seleccion)
    {
        _ruta = ruta;
        _arbol = Cargar(ruta);
        _deshacer.Clear(); _rehacer.Clear();
        _lienzo.Seleccion = null;
        Mostrar(seleccion == null);
        if (seleccion != null && _arbol.Buscar(seleccion) != null)
        {
            _lienzo.Seleccion = seleccion;
            _lienzo.CentrarEn(seleccion, 0.8, false);
        }
    }

    // ---------- Cambios ----------

    /// <summary>Aplica un cambio al árbol: se puede deshacer, se guarda al momento y el dibujo se recoloca.</summary>
    private void Cambiar(Action<Arbol> cambio)
    {
        var antes = _arbol.Clonar();
        cambio(_arbol);
        _deshacer.Add(antes);
        if (_deshacer.Count > 60) _deshacer.RemoveAt(0);
        _rehacer.Clear();
        Guardar();
        _lienzo.Refrescar();
        ActualizarFicha();
    }

    private void Guardar()
    {
        try { App.Biblioteca.Guardar(_ruta, _arbol); }
        catch (Exception ex) { _ = Principal.Actual.Error("No se pudo guardar", ex); }
    }

    private void Deshacer(bool rehacer)
    {
        var de = rehacer ? _rehacer : _deshacer;
        var a = rehacer ? _deshacer : _rehacer;
        if (de.Count == 0) return;
        a.Add(_arbol);
        _arbol = de[^1];
        de.RemoveAt(de.Count - 1);
        Guardar();
        _lienzo.Mostrar(_arbol, false);
        ActualizarFicha();
        Principal.Actual.Aviso(rehacer ? "Rehecho" : "Deshecho", 1.5);
    }

    // ---------- Ficha de la persona seleccionada ----------

    private void ActualizarFicha()
    {
        var id = _lienzo.Seleccion;
        var p = id != null ? _arbol.Buscar(id) : null;
        _ficha.IsVisible = p != null;
        _encuadrar.IsVisible = p == null;
        if (p == null) { _ficha.Child = null; return; }

        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        // la foto, el nombre y la historia: tocarlos abre la ficha (la flecha lo indica)
        var cabecera = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand) };
        cabecera.Tapped += (_, e) => { e.Handled = true; _ = Editar(p.Id, false); };
        g.Children.Add(cabecera);
        var retrato = Ui.Retrato(p, 56 * Ui.EscalaIcono);
        retrato.Margin = new Thickness(0, 0, 14, 0);
        cabecera.Children.Add(retrato);
        var flecha = Ui.Icono(Ui.Iconos.Derecha, 22, Ui.Suave);
        flecha.VerticalAlignment = VerticalAlignment.Center; flecha.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(flecha, 2); cabecera.Children.Add(flecha);
        var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textos.Children.Add(new TextBlock { Text = Nombres.SinNombre(p) ? "Sin nombre" : p.Nombre, FontSize = Ui.L(20), FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrWhiteSpace(p.Apellidos)) textos.Children.Add(new TextBlock { Text = p.Apellidos, FontSize = Ui.L(15), Foreground = Ui.Suave, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrWhiteSpace(p.Historia))
            textos.Children.Add(new TextBlock { Text = p.Historia.Trim(), FontSize = Ui.L(14), Foreground = Ui.Suave, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(textos, 1); cabecera.Children.Add(textos);

        var botones = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        void Poner(Button b) { b.Margin = new Thickness(0, 0, 8, 4); botones.Children.Add(b); }
        Poner(Ui.Boton("Familiar", () => _ = AnadirFamiliar(p.Id), principal: true, icono: Ui.Iconos.Mas));
        if (!string.IsNullOrEmpty(p.ArbolEnlazado)) Poner(Ui.Boton("Su árbol", () => _ = AbrirEnlace(p.Id), icono: Ui.Iconos.Enlace));
        Grid.SetRow(botones, 1); g.Children.Add(botones);
        _ficha.Child = g;
    }

    // ---------- Editar y añadir ----------

    private async Task Editar(string id, bool nueva)
    {
        if (_arbol.Buscar(id) is not { } p) return;
        var r = await EditorPersona.Abrir(p, _arbol, nueva);
        if (r.Eliminar) { await Eliminar(id); return; }
        if (r.Persona is { } editada)
        {
            Cambiar(a =>
            {
                var q = a.Obtener(id);
                q.Nombre = editada.Nombre; q.Apellidos = editada.Apellidos; q.Sexo = editada.Sexo;
                q.Historia = editada.Historia; q.Foto = editada.Foto; q.ArbolEnlazado = editada.ArbolEnlazado;
            });
        }
        else if (nueva)
        {
            // al cancelar el editor de una persona recién añadida, se quita (como si no se hubiera añadido)
            Deshacer(false);
            _rehacer.Clear();
        }
    }

    private async Task Eliminar(string id)
    {
        var quitadas = _arbol.PersonasQueSeEliminarian(id);
        var p = _arbol.Obtener(id);
        string texto = quitadas.Count <= 1
            ? "Se quitará del árbol. Se puede deshacer."
            : $"También se quitarán {quitadas.Count - 1} persona(s) que solo estaban unidas al árbol a través de ella. Se puede deshacer.";
        if (await Principal.Actual.Preguntar("¿Eliminar a " + Nombres.Mostrar(p) + "?", texto, "Eliminar", "Cancelar") != 0) return;
        _lienzo.Seleccion = null;
        Cambiar(a => a.Eliminar(id));
    }

    private async Task AnadirFamiliar(string id)
    {
        if (_arbol.Buscar(id) is not { } p) return;
        _lienzo.Seleccion = id;
        bool sangre = _arbol.EsSanguinea(id);
        var padres = _arbol.UnionComoHijo(id);
        int nPadres = padres?.Parejas.Count ?? 0;
        var opPadres = new Opcion(nPadres == 1 ? "El padre o la madre que falta" : "Padres", Ui.Iconos.Persona,
            Activa: sangre && nPadres < 2, Detalle: sangre ? null : "Su familia va en su propio árbol");
        var opHermano = new Opcion("Hermano o hermana", Ui.Iconos.Persona, Activa: sangre, Detalle: sangre ? null : "Su familia va en su propio árbol");
        var opPareja = new Opcion("Pareja", Ui.Iconos.Persona);
        var opHijo = new Opcion("Hijo o hija", Ui.Iconos.Persona);
        var r = await Principal.Actual.Menu("Añadir familiar de " + Nombres.Mostrar(p), new[] { opPadres, opHermano, opPareja, opHijo });
        if (r == null) return;
        string? nuevo = null;
        if (r == opPadres) Cambiar(a => nuevo = a.AnadirPadres(id).FirstOrDefault()?.Id);
        else if (r == opHermano) Cambiar(a => nuevo = a.AnadirHermano(id).hermano.Id);
        else if (r == opPareja) Cambiar(a => nuevo = a.AnadirPareja(id).Id);
        else if (r == opHijo)
        {
            var uniones = _arbol.UnionesComoPareja(id).ToList();
            string? union = null;
            if (uniones.Count > 1)
            {
                var ops = uniones.Select(u =>
                {
                    var otro = u.Parejas.FirstOrDefault(x => x != id);
                    return new Opcion("Con " + (otro == null ? "pareja desconocida" : Nombres.Mostrar(_arbol.Buscar(otro))), Ui.Iconos.Persona);
                }).ToList();
                var e = await Principal.Actual.Menu("¿Hijo o hija de qué pareja?", ops);
                if (e == null) return;
                union = uniones[ops.IndexOf(e)].Id;
            }
            Cambiar(a => nuevo = a.AnadirHijo(id, union).Id);
        }
        if (nuevo != null)
        {
            _lienzo.Seleccion = nuevo;
            _lienzo.CentrarEn(nuevo, null, true);
            await Editar(nuevo, true);
        }
    }

    private async Task AbrirEnlace(string id)
    {
        var p = _arbol.Buscar(id);
        if (p?.ArbolEnlazado is not { } enlace) return;
        var ruta = App.Biblioteca.Resolver(enlace);
        if (ruta == null)
        {
            await Principal.Actual.Avisar("No tienes ese árbol",
                $"La tarjeta de {Nombres.Mostrar(p)} enlaza con el árbol «{Path.GetFileNameWithoutExtension(enlace)}», que no está en este teléfono. " +
                "Pide que te lo manden (al compartir un árbol se pueden mandar también los enlazados) y ábrelo: el enlace funcionará solo.");
            return;
        }
        _pila.Push((_ruta, id));
        CambiarDeArbol(ruta, null);
        Principal.Actual.Aviso("«Atrás» vuelve al árbol anterior", 2.2);
    }

    // ---------- Búsqueda ----------

    private void AbrirBusqueda()
    {
        _barra.IsVisible = false; _barraBusqueda.IsVisible = true;
        _buscar.Text = "";
        _buscar.Focus();
    }

    private void CerrarBusqueda()
    {
        _barraBusqueda.IsVisible = false; _barra.IsVisible = true;
        _coincidencias.Clear(); _actual = -1;
        _lienzo.ResaltarCoincidencias(Array.Empty<string>());
    }

    private void Buscar()
    {
        var encontradas = _arbol.BuscarPorNombre(_buscar.Text ?? "").Select(p => p.Id).ToList();
        var lay = _lienzo.Layout;
        _coincidencias = lay == null ? encontradas : encontradas.Where(lay.Cartas.ContainsKey)
            .OrderBy(id => Math.Round(lay.Cartas[id].Y)).ThenBy(id => lay.Cartas[id].X).ToList();
        _actual = -1;
        _contador.Text = string.IsNullOrWhiteSpace(_buscar.Text) ? "" : _coincidencias.Count == 0 ? "0" : $"{_coincidencias.Count}";
        _lienzo.ResaltarCoincidencias(_coincidencias);
        if (_coincidencias.Count > 0) IrACoincidencia(1);
    }

    private void IrACoincidencia(int paso)
    {
        if (_coincidencias.Count == 0) return;
        _actual = ((_actual + paso) % _coincidencias.Count + _coincidencias.Count) % _coincidencias.Count;
        var id = _coincidencias[_actual];
        _contador.Text = $"{_actual + 1}/{_coincidencias.Count}";
        _lienzo.Seleccion = id;
        _lienzo.CentrarEn(id, null, true);
    }

    // ---------- Pruebas (la ventana de escritorio, con --captura) ----------

    internal LienzoArbol Lienzo => _lienzo;
    internal string Ruta => _ruta;

    internal Task Prueba(string que, string? id)
    {
        if (que == "ficha") return Editar(id!, false);
        if (que == "familiar") return AnadirFamiliar(id!);
        if (que == "menu") return Menu();
        if (que.StartsWith("buscar:")) { AbrirBusqueda(); _buscar.Text = que[7..]; }
        return Task.CompletedTask;
    }

    // ---------- Menú ----------

    private async Task Menu()
    {
        var p = Principal.Actual;
        var compartir = new Opcion("Compartir el árbol", Ui.Iconos.Compartir, "El archivo .tree: por WhatsApp, correo, Bluetooth…");
        var imagen = new Opcion("Compartir como imagen", Ui.Iconos.Galeria, "Una foto de todo el árbol");
        var deshacer = new Opcion("Deshacer", Ui.Iconos.Deshacer, Activa: _deshacer.Count > 0);
        var rehacer = new Opcion("Rehacer", Ui.Iconos.Rehacer, Activa: _rehacer.Count > 0);
        var colocacion = new Opcion("Colocación: " + Nombres.Ordenacion(_lienzo.Ordenacion), Ui.Iconos.Colocacion);
        var forma = new Opcion("Forma de las tarjetas", Ui.Iconos.Tarjeta);
        var principal = new Opcion("Ir a la persona principal", Ui.Iconos.Casa);
        var fusionar = new Opcion("Fusionar con otro árbol…", Ui.Iconos.Fusionar, "Añadir lo que tenga otro árbol del teléfono");
        var renombrar = new Opcion("Cambiar el nombre del árbol", Ui.Iconos.Editar);
        var copia = new Opcion("Guardar una copia en…", Ui.Iconos.Guardar);
        var r = await p.Menu(null, new[] { compartir, imagen, Opcion.Linea, deshacer, rehacer, Opcion.Linea, colocacion, forma, principal, Opcion.Linea, fusionar, renombrar, copia });
        try
        {
            if (r == compartir) await Compartir.Arbol(_ruta, _titulo.Text ?? "");
            else if (r == imagen) await Compartir.Imagen(_lienzo, _titulo.Text ?? "árbol");
            else if (r == deshacer) Deshacer(false);
            else if (r == rehacer) Deshacer(true);
            else if (r == colocacion) await ElegirColocacion();
            else if (r == forma) await ElegirForma();
            else if (r == principal) { _lienzo.Seleccion = _arbol.RaizId; _lienzo.CentrarEn(_arbol.RaizId, Math.Max(0.7, 0.01)); }
            else if (r == fusionar) await FusionarCon();
            else if (r == renombrar) await Renombrar();
            else if (r == copia) await Compartir.GuardarCopia(this, _ruta, _titulo.Text ?? "arbol");
        }
        catch (Exception ex) { await p.Error("No se pudo completar", ex); }
    }

    private async Task ElegirColocacion()
    {
        var ops = new[]
        {
            (Ordenacion.C, "Los antepasados a los lados, sus familias repartidas"),
            (Ordenacion.B, "Como Family TreePhoto: cada antepasado con su rama"),
            (Ordenacion.A, "Como los árboles impresos: lo más junto posible"),
            (Ordenacion.D, "Estrecho, en escalera: para imprimir en una hoja"),
        };
        var opciones = ops.Select(o => new Opcion(Nombres.Ordenacion(o.Item1), Ui.Iconos.Colocacion, o.Item2, Marcada: o.Item1 == _lienzo.Ordenacion)).ToList();
        var r = await Principal.Actual.Menu("Colocación del árbol", opciones);
        if (r == null) return;
        _lienzo.Ordenacion = ops[opciones.IndexOf(r)].Item1;
        Preferencias.Actual.Ordenacion = _lienzo.Ordenacion.ToString();
        Preferencias.Actual.Guardar();
    }

    private async Task ElegirForma()
    {
        var f = _lienzo.Forma;
        var formas = new[]
        {
            (new FormaTarjeta(false, true, SinApellidos: f.SinApellidos), "Horizontales con foto", "La foto al lado del nombre"),
            (new FormaTarjeta(true, true, SinApellidos: f.SinApellidos), "Verticales con foto", "La foto encima: el árbol sale más estrecho"),
            (new FormaTarjeta(false, false, SinApellidos: f.SinApellidos), "Horizontales sin foto", "Más pequeñas: cabe más árbol"),
            (new FormaTarjeta(true, false, SinApellidos: f.SinApellidos), "Verticales sin foto", "Las más estrechas"),
        };
        var opciones = formas.Select(x => new Opcion(x.Item2, Ui.Iconos.Tarjeta, x.Item3, Marcada: x.Item1.Vertical == f.Vertical && x.Item1.Foto == f.Foto)).ToList();
        var apellidos = new Opcion(f.SinApellidos ? "Mostrar los apellidos" : "Ocultar los apellidos", Ui.Iconos.Editar);
        opciones.Add(Opcion.Linea);
        opciones.Add(apellidos);
        var r = await Principal.Actual.Menu("Forma de las tarjetas", opciones);
        if (r == null) return;
        _lienzo.Forma = r == apellidos ? f with { SinApellidos = !f.SinApellidos } : formas[opciones.IndexOf(r)].Item1;
        Preferencias.Actual.Forma = _lienzo.Forma.ToString();
        Preferencias.Actual.Guardar();
    }

    private async Task Renombrar()
    {
        var nombre = await Principal.Actual.PedirTexto("Nombre del árbol", _titulo.Text ?? "");
        if (string.IsNullOrWhiteSpace(nombre) || nombre == _titulo.Text) return;
        _ruta = App.Biblioteca.Renombrar(_ruta, nombre);
        _arbol.Nombre = nombre;
        _titulo.Text = nombre;
    }

    private async Task FusionarCon()
    {
        var lista = App.Biblioteca.Listar().Where(e => !string.Equals(e.Ruta, _ruta, StringComparison.OrdinalIgnoreCase)).ToList();
        if (lista.Count == 0) { await Principal.Actual.Avisar("No hay otros árboles", "En «Mis árboles» no hay ningún otro árbol con el que fusionar este."); return; }
        var ops = lista.Select(e => new Opcion(e.Nombre, Ui.Iconos.Fusionar, Principal.Personas(e.Personas))).ToList();
        var r = await Principal.Actual.Menu("¿Qué árbol añades a este?", ops);
        if (r == null) return;
        var otro = ArbolJson.Cargar(lista[ops.IndexOf(r)].Ruta, out _);
        await PantallaFusion.Fusionar(_ruta, otro, lista[ops.IndexOf(r)].Nombre);
    }
}
