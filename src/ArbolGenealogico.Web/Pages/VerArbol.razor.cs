using ArbolGenealogico.Core.Io;
using ArbolGenealogico.Core.Layout;
using ArbolGenealogico.Core.Model;
using ArbolGenealogico.Web.Servicios;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ArbolGenealogico.Web.Pages;

public partial class VerArbol : IAsyncDisposable
{
    [Parameter] public string Id { get; set; } = "";
    [Inject] private Datos Datos { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    // ---------- Estado ----------
    private Arbol? _a;
    private string _rol = "lector";
    private int _version;
    private string? _errorCarga;
    private string? _cargadoId;
    private bool Editable => _rol is "propietario" or "editor";
    private bool Propietario => _rol == "propietario";

    private LayoutResult? _layout;
    private FormaTarjeta _forma = FormaTarjeta.Ancha;       // la elegida
    private FormaTarjeta _formaUso = FormaTarjeta.Ancha;    // la elegida, más SoloNombre si toca
    private Ordenacion _ordenacion = Ordenacion.C;
    private HashSet<string> _directa = new();
    private HashSet<string> _sanguineos = new();
    private HashSet<Conexion> _linaje = new(), _cercanas = new();
    private HashSet<string>? _familia;
    private string? _sel;

    private string _busqueda = "";
    private List<string> _coincidencias = new();
    private HashSet<string> _coincide = new();
    private int _indiceCoincidencia = -1;
    private bool _buscando;     // en el móvil, la barra pasa a ser el buscador

    private readonly List<Arbol> _deshacer = new(), _rehacer = new();
    private const int MaxDeshacer = 60;

    private bool _sucio, _guardando;
    private int _cambios, _cambiosGuardados;
    private string? _errorGuardado;
    private System.Threading.Timer? _temporizador;
    private DateTime _ultimoGuardado;

    // ---------- Interfaz ----------
    private ElementReference _vp, _mundo;
    private IJSObjectReference? _v;
    private DotNetObjectReference<VerArbol>? _ref;
    private bool _vistaHecha, _sinAnimar = true, _exportando, _temaClaro, _conTitulo;
    private (double X, double Y)? _anclaAntes;
    private string? _anclaId;

    private string? _menuPersona; private double _menuX, _menuY;
    private bool _menuGeneral, _panelForma, _atajos, _compartir, _renombrar, _imagen, _imprimir;
    private string? _editando; private bool _editandoNueva;
    private string? _confirmarBorrar;
    private string? _enlazarPersona;
    private List<ResumenArbol>? _arbolesConocidos;
    private bool _conflicto;
    private string? _conflictoPor;
    private string? _toast; private System.Threading.Timer? _tToast;
    private string _nombreNuevo = "";
    private bool _ocupado;
    private int _peticiones;

    /// <summary>Cuántas personas piden acceso a este árbol (solo lo ve el propietario).</summary>
    private async Task ContarPeticiones()
    {
        if (!Propietario || Datos.Local) { _peticiones = 0; return; }
        try { _peticiones = (await Datos.MisSolicitudes()).Count(x => x.ArbolId == Id); }
        catch { _peticiones = 0; }
        StateHasChanged();
    }

    private async Task CerrarCompartir()
    {
        _compartir = false;
        await ContarPeticiones();
    }

    // ---------- Carga ----------
    protected override async Task OnInitializedAsync()
    {
        _ref = DotNetObjectReference.Create(this);
        await Datos.Iniciar();
        _forma = FormaTarjeta.Leer(await Pref("forma"));
        if (Enum.TryParse<Ordenacion>(await Pref("ordenacion"), out var o)) _ordenacion = o;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_cargadoId == Id) return;
        if (_cargadoId != null) await GuardarAhora();
        _cargadoId = Id;
        _a = null; _layout = null; _sel = null; _errorCarga = null; _vistaHecha = false; _sinAnimar = true;
        _deshacer.Clear(); _rehacer.Clear(); _busqueda = ""; _coincidencias.Clear(); _coincide.Clear();
        _sucio = false; _cambios = _cambiosGuardados = 0; _errorGuardado = null; _conflicto = false;
        CerrarTodo();
        if (Datos.Actual == null) { Nav.NavigateTo(""); return; }
        try
        {
            var r = await Datos.Cargar(Id);
            if (r == null) { _errorCarga = "No existe este árbol, o no tienes acceso. Si un familiar lo ha compartido contigo, comprueba que entraste con el correo al que te invitó."; return; }
            _a = ArbolJson.Deserializar(r.Datos, out _);
            if (string.IsNullOrWhiteSpace(_a.Nombre)) _a.Nombre = r.Nombre;
            _rol = r.Rol; _version = r.Version;
            Recolocar();
            _ = ContarPeticiones();
        }
        catch (Exception e) { _errorCarga = "No se ha podido abrir el árbol: " + e.Texto(); }
    }

    protected override async Task OnAfterRenderAsync(bool primera)
    {
        if (_a == null || _layout == null) return;
        _v ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/vista.js");
        if (!_vistaHecha)
        {
            _vistaHecha = true;
            await _v.InvokeVoidAsync("crear", _vp, _mundo, _ref, "vista." + Id);
            await _v.InvokeVoidAsync("editable", Editable);
            await _v.InvokeVoidAsync("tamano", _layout.Ancho, _layout.Alto);
            if (!Datos.Local) await Datos.Suscribir(Id, _ref!);
            var guardada = await _v.InvokeAsync<double[]?>("vistaGuardada");
            if (guardada is { Length: 3 }) await _v.InvokeVoidAsync("ponerVista", guardada[0], guardada[1], guardada[2]);
            else await VistaInicial();
            await _v.InvokeVoidAsync("ajustarTextos");
            _sinAnimar = false;
            StateHasChanged();
            return;
        }
        await _v.InvokeVoidAsync("tamano", _layout.Ancho, _layout.Alto);
        await _v.InvokeVoidAsync("ajustarTextos");
        if (_anclaAntes is { } antes && _anclaId != null && _layout.Cartas.TryGetValue(_anclaId, out var c))
        {
            var z = (await _v.InvokeAsync<double[]>("vista"))[2];
            await _v.InvokeVoidAsync("desplazar", (antes.X - c.X) * z, (antes.Y - c.Y) * z, !_sinAnimar);
        }
        _anclaAntes = null; _anclaId = null;
        if (_sinAnimar) { _sinAnimar = false; StateHasChanged(); }
    }

    /// <summary>La primera vez: el árbol entero si cabe con un zoom legible; si no, la persona principal con su familia cercana.</summary>
    private async Task VistaInicial()
    {
        if (_v == null || _layout == null || _a == null) return;
        var (w, h) = await Pantalla();
        var o = _layout.Opciones;
        double z = Math.Min((w - 80) / _layout.Ancho, (h - 140) / _layout.Alto);
        if (z >= 0.42 || !_layout.Cartas.TryGetValue(_a.RaizId, out var raiz))
            await _v.InvokeVoidAsync("verZona", 0, 0, _layout.Ancho, _layout.Alto, 1.0, false);
        else
            await _v.InvokeVoidAsync("verZona", raiz.X - o.AnchoCarta * 2.5, raiz.Y - (o.AltoCarta + o.HuecoFilas) * 2, o.AnchoCarta * 6, (o.AltoCarta + o.HuecoFilas) * 4, 0.8, false);
    }

    private async Task<(double, double)> Pantalla()
    {
        var t = await _v!.InvokeAsync<double[]>("tamanoPantalla");
        return (t[0], t[1]);
    }

    /// <summary>
    /// Vuelve a colocar el árbol. La persona seleccionada (o la que se esté viendo en el centro) se queda en el mismo sitio
    /// de la pantalla mientras el resto se desliza a su sitio nuevo.
    /// </summary>
    private void Recolocar(string? ancla = null)
    {
        if (_a == null) return;
        ancla ??= _sel;
        if (_layout != null && ancla != null && _layout.Cartas.TryGetValue(ancla, out var antes))
        {
            _anclaAntes = (antes.X, antes.Y); _anclaId = ancla;
        }
        _formaUso = _forma.Para(_a);
        _layout = LayoutEngine.Calcular(_a, _formaUso.Opciones(_ordenacion));
        _directa = _a.LineaDirecta();
        _sanguineos = _a.Sanguineos();
        if (_sel != null && !_layout.Cartas.ContainsKey(_sel)) _sel = null;
        ActualizarResaltado();
        ActualizarBusqueda(false);
    }

    private void ActualizarResaltado()
    {
        if (_a == null || _layout == null) return;
        (_linaje, _cercanas, _familia) = Dibujo.Resaltado(_a, _layout, _exportando ? null : _sel);
    }

    /// <summary>La persona más cercana al centro de la pantalla (para que no se mueva al cambiar la colocación).</summary>
    private async Task<string?> PersonaDelCentro()
    {
        if (_v == null || _layout == null) return null;
        var c = await _v.InvokeAsync<double[]>("centro");
        double h = _layout.Opciones.AltoCarta;
        return _layout.Cartas.Values.OrderBy(p => Math.Pow(p.X - c[0], 2) + Math.Pow(p.Y + h / 2 - c[1], 2)).FirstOrDefault()?.Id;
    }

    private async Task CambiarOrdenacion(Ordenacion o)
    {
        if (o == _ordenacion) return;
        var ancla = _sel ?? await PersonaDelCentro();
        _ordenacion = o;
        await GuardarPref("ordenacion", o.ToString());
        Recolocar(ancla);
    }

    private async Task CambiarForma(FormaTarjeta f)
    {
        if (f == _forma) return;
        var ancla = _sel ?? await PersonaDelCentro();
        _forma = f;
        await GuardarPref("forma", f.ToString());
        Recolocar(ancla);
    }

    private async Task<string?> Pref(string clave) =>
        await (await Vista()).InvokeAsync<string?>("leerPreferencia", clave);

    private async Task GuardarPref(string clave, string valor) =>
        await (await Vista()).InvokeVoidAsync("guardarPreferencia", clave, valor);

    private async Task<IJSObjectReference> Vista() => _v ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/vista.js");

    // ---------- Selección y navegación ----------
    private void Seleccionar(string? id, bool centrar = false)
    {
        _sel = id;
        ActualizarResaltado();
        if (centrar && id != null) _ = Centrar(id);
    }

    private async Task Centrar(string id, bool siFuera = true)
    {
        if (_v == null || _layout == null || !_layout.Cartas.TryGetValue(id, out var c)) return;
        var o = _layout.Opciones;
        await _v.InvokeVoidAsync("centrarEn", c.X - o.AnchoCarta / 2, c.Y, o.AnchoCarta, o.AltoCarta, 0.55, siFuera, true);
    }

    private string? Vecino(string tecla)
    {
        if (_a == null || _layout == null) return null;
        if (_sel == null || !_layout.Cartas.TryGetValue(_sel, out var yo)) return _a.RaizId;
        var cartas = _layout.Cartas;
        string? MasCerca(IEnumerable<string> ids) => ids.Where(cartas.ContainsKey).OrderBy(i => Math.Abs(cartas[i].X - yo.X)).FirstOrDefault();
        switch (tecla)
        {
            case "ArrowUp":
                return MasCerca(_a.UnionComoHijo(_sel)?.Parejas ?? new List<string>());
            case "ArrowDown":
                return MasCerca(_a.UnionesComoPareja(_sel).SelectMany(u => u.Hijos));
            default:
                int signo = tecla == "ArrowRight" ? 1 : -1;
                double alto = _layout.Opciones.AltoCarta;
                var lado = cartas.Values.Where(c => c.Id != _sel && (c.X - yo.X) * signo > 1).ToList();
                var fila = lado.Where(c => Math.Abs(c.Y - yo.Y) < alto / 2).OrderBy(c => Math.Abs(c.X - yo.X)).FirstOrDefault();
                return (fila ?? lado.OrderBy(c => Math.Abs(c.X - yo.X) + Math.Abs(c.Y - yo.Y) * 3).FirstOrDefault())?.Id;
        }
    }

    // ---------- Eventos del lienzo (desde vista.js) ----------
    [JSInvokable]
    public void Tocar(string id)
    {
        _menuPersona = null;
        Seleccionar(id);
        StateHasChanged();
    }

    [JSInvokable]
    public void DobleToque(string id)
    {
        Seleccionar(id);
        AbrirEditor(id, false);
        StateHasChanged();
    }

    [JSInvokable]
    public void Fondo()
    {
        if (_menuPersona != null || _menuGeneral || _panelForma) { CerrarTodo(); StateHasChanged(); return; }
        Seleccionar(null);
        StateHasChanged();
    }

    [JSInvokable]
    public void Menu(string id, double x, double y)
    {
        Seleccionar(id);
        AbrirMenuPersona(id, x, y);
        StateHasChanged();
    }

    [JSInvokable]
    public void Enlace(string id, double x, double y)
    {
        Seleccionar(id);
        _ = AbrirEnlace(id);
        StateHasChanged();
    }

    [JSInvokable]
    public void Aviso(string texto) { Toast(texto); StateHasChanged(); }

    [JSInvokable]
    public void Redimensionado() { }

    [JSInvokable]
    public void FotoSoltada(string id, string foto)
    {
        if (!Editable || _a?.Buscar(id) is not { } p) return;
        Instantanea();
        p.Foto = foto;
        Cambiado();
        Seleccionar(id);
        Toast("Foto puesta a " + Nombres.Mostrar(p) + ".");
        StateHasChanged();
    }

    [JSInvokable]
    public void FotoPegada(string foto)
    {
        if (_editando != null) { _fotoEditor = foto; StateHasChanged(); return; }
        if (_sel == null) { Toast("Selecciona antes a la persona a la que quieres poner la foto."); StateHasChanged(); return; }
        FotoSoltada(_sel, foto);
    }

    private bool HayDialogo => _editando != null || _confirmarBorrar != null || _compartir || _renombrar || _imagen || _imprimir || _atajos || _conflicto || _enlazarPersona != null;

    [JSInvokable]
    public bool Tecla(string tecla, bool ctrl, bool mayus, bool alt)
    {
        try { return TeclaInterna(tecla, ctrl, mayus, alt); }
        catch (Exception e) { Toast(e.Texto()); StateHasChanged(); return true; }
    }

    private bool TeclaInterna(string tecla, bool ctrl, bool mayus, bool alt)
    {
        if (tecla == "Escape")
        {
            if (HayDialogo || _menuPersona != null || _menuGeneral || _panelForma) CerrarTodo();
            else if (_busqueda.Length > 0) { _busqueda = ""; ActualizarBusqueda(false); _buscando = false; }
            else Seleccionar(null);
            StateHasChanged();
            return true;
        }
        if (HayDialogo || _a == null) return false;
        if (_menuPersona != null && tecla.Length == 1 && char.IsDigit(tecla[0]))
        {
            var items = ItemsMenuPersona(_menuPersona).Where(i => !i.Separador && i.Habilitado).ToList();
            int n = tecla[0] - '1';
            if (n >= 0 && n < items.Count) { _menuPersona = null; items[n].Accion?.Invoke(); StateHasChanged(); }
            return true;
        }
        string k = tecla.Length == 1 ? tecla.ToLowerInvariant() : tecla;
        bool hecho = true;
        switch (k)
        {
            case "z" when ctrl && !mayus: Deshacer(); break;
            case "z" when ctrl && mayus:
            case "y" when ctrl: Rehacer(); break;
            case "f" when ctrl:
            case "/":
                _buscando = true; _ = Enfocar("#buscar"); break;
            case "F3": Siguiente(mayus ? -1 : 1); break;
            case "0" when ctrl: _ = VerTodo(); break;
            case "l" when ctrl: _ = CambiarOrdenacion((Ordenacion)(((int)_ordenacion + 1) % 4)); break;
            case "t" when ctrl && !mayus: _ = CambiarForma(_forma with { Vertical = !_forma.Vertical }); break;
            case "f" when ctrl && mayus: _ = CambiarForma(_forma with { Foto = !_forma.Foto }); break;
            case "t" when ctrl && mayus: _ = CambiarForma(_forma with { SinTarjeta = !_forma.SinTarjeta }); break;
            case "a" when ctrl && mayus: _ = CambiarForma(_forma with { SinApellidos = !_forma.SinApellidos }); break;
            case "s" when ctrl: _ = GuardarAhora(); Toast(_sucio ? "Guardando…" : "Todo está guardado."); break;
            case "e" when ctrl: _imagen = true; break;
            case "p" when ctrl: _imprimir = true; break;
            case "Home": Seleccionar(_a.RaizId, true); break;
            case "F1":
            case "?": _atajos = true; break;
            case "ArrowUp" or "ArrowDown" or "ArrowLeft" or "ArrowRight" when !ctrl && !alt:
                var v = Vecino(tecla);
                if (v != null) Seleccionar(v, true);
                break;
            case "Enter" when _sel != null && ctrl: _ = AbrirEnlace(_sel); break;
            case "Enter" or "F2" when _sel != null: AbrirEditor(_sel, false); break;
            case "Insert" or "+" when _sel != null && Editable: AbrirMenuPersona(_sel, null, null); break;
            case "Delete" when _sel != null && Editable && _sel != _a.RaizId: _confirmarBorrar = _sel; break;
            default: hecho = false; break;
        }
        if (hecho) StateHasChanged();
        return hecho;
    }

    private async Task Enfocar(string selector) { await Task.Delay(10); await (await Vista()).InvokeVoidAsync("enfocar", selector); }

    private async Task VerTodo()
    {
        if (_v == null || _layout == null) return;
        await _v.InvokeVoidAsync("verZona", 0, 0, _layout.Ancho, _layout.Alto, 1.2, true);
    }

    private async Task Zoom(double f) { if (_v != null) await _v.InvokeVoidAsync("zoom", f); }

    // ---------- Búsqueda ----------
    private void AlBuscar(ChangeEventArgs e)
    {
        _busqueda = e.Value?.ToString() ?? "";
        ActualizarBusqueda(true);
    }

    private void ActualizarBusqueda(bool irALaPrimera)
    {
        if (_a == null || _layout == null) return;
        var cartas = _layout.Cartas;
        _coincidencias = _a.BuscarPorNombre(_busqueda).Select(p => p.Id).Where(cartas.ContainsKey)
            .OrderBy(i => cartas[i].Y).ThenBy(i => cartas[i].X).ToList();
        _coincide = _coincidencias.ToHashSet();
        _indiceCoincidencia = -1;
        if (irALaPrimera && _coincidencias.Count > 0) Siguiente(1);
    }

    private void Siguiente(int paso)
    {
        if (_coincidencias.Count == 0) return;
        _indiceCoincidencia = ((_indiceCoincidencia + paso) % _coincidencias.Count + _coincidencias.Count) % _coincidencias.Count;
        Seleccionar(_coincidencias[_indiceCoincidencia], true);
    }

    private void TeclaBuscador(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "F3") Siguiente(e.ShiftKey ? -1 : 1);
        else if (e.Key == "Escape") { _busqueda = ""; ActualizarBusqueda(false); _buscando = false; }
    }

    // ---------- Edición ----------
    private void Instantanea()
    {
        if (_a == null) return;
        _deshacer.Add(_a.Clonar());
        if (_deshacer.Count > MaxDeshacer) _deshacer.RemoveAt(0);
        _rehacer.Clear();
    }

    private void Cambiado(string? ancla = null)
    {
        _cambios++;
        _sucio = true;
        Recolocar(ancla);
        ProgramarGuardado();
    }

    private void Deshacer()
    {
        if (!Editable || _deshacer.Count == 0 || _a == null) return;
        _rehacer.Add(_a);
        _a = _deshacer[^1]; _deshacer.RemoveAt(_deshacer.Count - 1);
        Cambiado();
    }

    private void Rehacer()
    {
        if (!Editable || _rehacer.Count == 0 || _a == null) return;
        _deshacer.Add(_a);
        _a = _rehacer[^1]; _rehacer.RemoveAt(_rehacer.Count - 1);
        Cambiado();
    }

    private void Anadir(Func<Arbol, Persona> op)
    {
        if (_a == null || !Editable) return;
        try
        {
            Instantanea();
            var ancla = _sel;
            var p = op(_a);
            Cambiado(ancla);
            _sel = p.Id;
            ActualizarResaltado();
            AbrirEditor(p.Id, true);
        }
        catch (InvalidOperationException e)
        {
            _deshacer.RemoveAt(_deshacer.Count - 1);
            Toast(e.Texto());
        }
    }

    private void Eliminar(string id)
    {
        if (_a == null || !Editable) return;
        _confirmarBorrar = null;
        Instantanea();
        var quitadas = _a.Eliminar(id);
        _sel = null;
        Cambiado();
        Toast(quitadas.Count == 1 ? "Persona eliminada. Ctrl+Z (o Deshacer) la recupera." : $"{quitadas.Count} personas eliminadas. Ctrl+Z (o Deshacer) las recupera.");
    }

    // ---------- Guardado ----------
    private void ProgramarGuardado()
    {
        _ = (_v ?? null)?.InvokeVoidAsync("avisarSalida", true);
        _temporizador?.Dispose();
        _temporizador = new System.Threading.Timer(_ => InvokeAsync(GuardarAhora), null, 1200, Timeout.Infinite);
    }

    private async Task GuardarAhora()
    {
        if (_a == null || !_sucio || _guardando || _conflicto || !Editable) return;
        _guardando = true;
        StateHasChanged();
        int cambios = _cambios;
        try
        {
            var r = await Datos.Guardar(Id, _a.Nombre, ArbolJson.Serializar(_a), _version);
            if (r == -1)
            {
                _conflicto = true;
                _conflictoPor = null;
            }
            else
            {
                _version = r;
                _cambiosGuardados = cambios;
                _errorGuardado = null;
                _ultimoGuardado = DateTime.Now;
                if (_cambios == cambios) _sucio = false;
            }
        }
        catch (Exception e)
        {
            _errorGuardado = e.Texto();
            _temporizador?.Dispose();
            _temporizador = new System.Threading.Timer(_ => InvokeAsync(GuardarAhora), null, 6000, Timeout.Infinite);
        }
        finally
        {
            _guardando = false;
            if (_v != null) await _v.InvokeVoidAsync("avisarSalida", _sucio);
            StateHasChanged();
        }
        if (_sucio && !_conflicto && _errorGuardado == null) ProgramarGuardado();
    }

    private string EstadoGuardado =>
        !Editable ? (Datos.Local ? "" : "Solo lectura")
        : _conflicto ? "Conflicto: otra persona ha guardado"
        : _errorGuardado != null ? "Sin guardar: " + _errorGuardado
        : _guardando ? "Guardando…"
        : _sucio ? "Cambios sin guardar"
        : _ultimoGuardado != default ? "Guardado" : Datos.Local ? "Guardado en este navegador" : "Guardado en la nube";

    [JSInvokable]
    public async Task CambioRemoto(int version, string por)
    {
        if (version <= _version || _a == null) return;
        if (_sucio || _guardando) { _conflictoPor = por; return; }   // al guardar se avisará del conflicto
        await RecargarDelServidor($"Actualizado con los cambios de {(string.IsNullOrEmpty(por) ? "otro familiar" : por)}.");
    }

    private async Task RecargarDelServidor(string? aviso)
    {
        var r = await Datos.Cargar(Id);
        if (r == null || _a == null) return;
        var sel = _sel;
        _a = ArbolJson.Deserializar(r.Datos, out _);
        if (string.IsNullOrWhiteSpace(_a.Nombre)) _a.Nombre = r.Nombre;
        _version = r.Version; _rol = r.Rol;
        _sucio = false; _conflicto = false; _errorGuardado = null;
        _deshacer.Clear(); _rehacer.Clear();
        _sel = sel != null && _a.Buscar(sel) != null ? sel : null;
        Recolocar(_sel);
        if (_v != null) { await _v.InvokeVoidAsync("editable", Editable); await _v.InvokeVoidAsync("avisarSalida", false); }
        if (aviso != null) Toast(aviso);
        StateHasChanged();
    }

    private async Task UsarLaSuya() { _conflicto = false; await RecargarDelServidor("Cargada la versión guardada por la otra persona."); }

    private async Task UsarLaMia()
    {
        if (_a == null) return;
        _conflicto = false;
        try
        {
            var r = await Datos.Guardar(Id, _a.Nombre, ArbolJson.Serializar(_a), null);
            _version = r; _sucio = false; _ultimoGuardado = DateTime.Now; _errorGuardado = null;
            Toast("Guardada tu versión.");
        }
        catch (Exception e) { _errorGuardado = e.Texto(); }
    }

    // ---------- Árboles enlazados ----------
    private async Task AbrirEnlace(string id)
    {
        var p = _a?.Buscar(id);
        if (p == null || string.IsNullOrEmpty(p.ArbolEnlazado)) return;
        if (Enlaces.EsWeb(p.ArbolEnlazado))
        {
            await GuardarAhora();
            Nav.NavigateTo("arbol/" + p.ArbolEnlazado);
            return;
        }
        await PedirEnlace(id);
    }

    private async Task PedirEnlace(string id)
    {
        _enlazarPersona = id;
        _menuPersona = null;
        try { _arbolesConocidos = (await Datos.Listar()).Where(r => r.Id != Id).ToList(); }
        catch (Exception e) { _arbolesConocidos = new(); Toast(e.Texto()); }
        StateHasChanged();
    }

    private void Enlazar(string personaId, string? arbolId)
    {
        if (_a?.Buscar(personaId) is not { } p) return;
        Instantanea();
        p.ArbolEnlazado = arbolId;
        _enlazarPersona = null;
        Cambiado();
        Toast(arbolId == null ? "Enlace quitado." : "Tarjeta enlazada con su árbol.");
    }

    /// <summary>
    /// Crea el árbol propio de una persona con su familia de sangre (y los cónyuges), lo enlaza con este y lo abre. Los
    /// familiares que ven este árbol también podrán ver el nuevo.
    /// </summary>
    private async Task CrearArbolPropio(string id)
    {
        if (_a?.Buscar(id) is not { } p || !Editable) return;
        _menuPersona = null; _editando = null;
        _ocupado = true;
        StateHasChanged();
        try
        {
            await GuardarAhora();
            var nuevo = _a.ExtraerFamiliaDe(id);
            if (nuevo.Buscar(_a.RaizId) is { } dueno) dueno.ArbolEnlazado = Id;
            var nombre = string.IsNullOrWhiteSpace(p.NombreCompleto) ? "Árbol nuevo" : "Familia de " + p.NombreCompleto;
            nuevo.Nombre = nombre;
            var nuevoId = await Datos.Crear(nombre, ArbolJson.Serializar(nuevo));
            try { await Datos.CopiarMiembros(Id, nuevoId); } catch { /* sin permiso para copiar: solo lo verá quien lo crea */ }
            Instantanea();
            p.ArbolEnlazado = nuevoId;
            _cambios++; _sucio = true;
            await GuardarAhora();
            Nav.NavigateTo("arbol/" + nuevoId);
        }
        catch (Exception e) { Toast("No se ha podido crear el árbol: " + e.Texto()); }
        finally { _ocupado = false; }
    }

    // ---------- Menús ----------
    private sealed record Item(MarkupString Icono, string Texto, Action? Accion, bool Habilitado = true, string? Ayuda = null, bool Peligro = false, bool Separador = false)
    {
        public static Item Sep() => new(default, "", null, Separador: true);
    }

    private void AbrirMenuPersona(string id, double? x, double? y)
    {
        _menuGeneral = false; _panelForma = false;
        _menuPersona = id;
        _ = PosicionarMenu(x, y);
    }

    private async Task PosicionarMenu(double? x, double? y)
    {
        var (w, h) = _v != null ? await Pantalla() : (1000, 800);
        double mx = x ?? w / 2, my = y ?? h / 2;
        _menuX = Math.Max(8, Math.Min(mx - 20, w - 300));
        int n = _menuPersona != null ? ItemsMenuPersona(_menuPersona).Count : 8;
        double alto = n * 42 + 50;
        _menuY = my + alto > h - 8 ? Math.Max(8, my - alto) : my + 6;
        StateHasChanged();
    }

    private List<Item> ItemsMenuPersona(string id)
    {
        var items = new List<Item>();
        if (_a?.Buscar(id) is not { } p) return items;
        if (Editable)
        {
            bool sang = _sanguineos.Contains(id);
            const string ayudaPolitica = "Su familia va en su propio árbol: usa «Crear su árbol con su familia».";
            var up = _a.UnionComoHijo(id);
            if (up == null || up.Parejas.Count < 2)
            {
                string txt = up == null ? "Añadir padres" : _a.Obtener(up.Parejas[0]).Sexo == Sexo.Hombre ? "Añadir madre" : "Añadir padre";
                items.Add(new(Iconos.Padres, txt, () => Anadir(x => x.AnadirPadres(id)[0]), sang, sang ? null : ayudaPolitica));
            }
            items.Add(new(Iconos.Hermano, "Añadir hermano/a", () => Anadir(x => x.AnadirHermano(id).hermano), sang, sang ? null : ayudaPolitica));
            items.Add(new(Iconos.Pareja, "Añadir pareja", () => Anadir(x => x.AnadirPareja(id))));
            var uniones = _a.UnionesComoPareja(id).ToList();
            if (uniones.Count <= 1)
                items.Add(new(Iconos.Hijo, "Añadir hijo/a", () => Anadir(x => x.AnadirHijo(id, null))));
            else
            {
                foreach (var u in uniones)
                {
                    var otro = u.Parejas.Where(q => q != id).Select(q => _a.Buscar(q)).FirstOrDefault();
                    var uid = u.Id;
                    items.Add(new(Iconos.Hijo, $"Añadir hijo/a con {Nombres.Mostrar(otro)}", () => Anadir(x => x.AnadirHijo(id, uid))));
                }
                items.Add(new(Iconos.Hijo, "Añadir hijo/a con otra pareja", () => Anadir(x => { var u = x.NuevaUnion(id); return x.AnadirHijo(id, u.Id); })));
            }
            items.Add(Item.Sep());
        }
        items.Add(new(Editable ? Iconos.Editar : Iconos.Historia, Editable ? "Editar…" : "Ver ficha…", () => AbrirEditor(id, false)));
        if (id != _a.RaizId)
        {
            if (!string.IsNullOrEmpty(p.ArbolEnlazado))
                items.Add(new(Iconos.Enlace, "Abrir su árbol", () => _ = AbrirEnlace(id)));
            else if (Editable)
            {
                items.Add(new(Iconos.Arbol, "Crear su árbol con su familia…", () => _ = CrearArbolPropio(id)));
                items.Add(new(Iconos.Enlace, "Enlazar con un árbol que ya existe…", () => _ = PedirEnlace(id)));
            }
            if (Editable)
            {
                items.Add(Item.Sep());
                items.Add(new(Iconos.Borrar, "Eliminar…", () => _confirmarBorrar = id, Peligro: true));
            }
        }
        return items;
    }

    private void Ejecutar(Item it)
    {
        _menuPersona = null; _menuGeneral = false;
        it.Accion?.Invoke();
    }

    private void CerrarTodo()
    {
        _menuPersona = null; _menuGeneral = false; _panelForma = false; _atajos = false; _compartir = false;
        _renombrar = false; _imagen = false; _imprimir = false; _editando = null; _confirmarBorrar = null; _enlazarPersona = null;
    }

    private void Toast(string texto)
    {
        _toast = texto;
        _tToast?.Dispose();
        _tToast = new System.Threading.Timer(_ => InvokeAsync(() => { _toast = null; StateHasChanged(); }), null, 4000, Timeout.Infinite);
    }

    // ---------- Editor de persona ----------
    private string _nomEditor = "", _apeEditor = "", _histEditor = "";
    private Sexo _sexoEditor;
    private string? _fotoEditor;

    private void AbrirEditor(string id, bool nueva)
    {
        if (_a?.Buscar(id) is not { } p) return;
        _menuPersona = null;
        _editando = id; _editandoNueva = nueva;
        _nomEditor = p.Nombre; _apeEditor = p.Apellidos; _histEditor = p.Historia; _sexoEditor = p.Sexo; _fotoEditor = p.Foto;
        if (Editable) _ = Enfocar("#ed-nombre");
    }

    private void GuardarEditor()
    {
        if (_editando == null || _a?.Buscar(_editando) is not { } p) { _editando = null; return; }
        bool cambia = p.Nombre != _nomEditor.Trim() || p.Apellidos != _apeEditor.Trim() || p.Historia != _histEditor || p.Sexo != _sexoEditor || p.Foto != _fotoEditor;
        if (cambia && Editable)
        {
            // la persona recién añadida ya tiene su instantánea (la de antes de añadirla): deshacer la quita del todo
            if (!_editandoNueva) Instantanea();
            p.Nombre = _nomEditor.Trim(); p.Apellidos = _apeEditor.Trim(); p.Historia = _histEditor; p.Sexo = _sexoEditor; p.Foto = _fotoEditor;
            Cambiado(_editando);
        }
        _editando = null;
    }

    private async Task ElegirFoto()
    {
        var f = await (await Vista()).InvokeAsync<string?>("elegirFoto");
        if (f != null) _fotoEditor = f;
    }

    private void TeclaEditor(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") _editando = null;
    }

    // ---------- Descargas e impresión ----------
    private async Task DescargarJson()
    {
        if (_a == null) return;
        _menuGeneral = false;
        List<ResumenArbol> conocidos;
        try { conocidos = await Datos.Listar(); } catch { conocidos = new(); }
        await (await Vista()).InvokeVoidAsync("descargar", Enlaces.ArchivoPara(_a.Nombre), Enlaces.ParaEscritorio(_a, conocidos), "application/json");
    }

    private string _temaImagen = "oscuro", _formatoImagen = "png";
    private double _escalaImagen = 1;
    private bool _transparente;
    private bool _apaisado = true;

    private async Task GuardarImagen()
    {
        if (_a == null || _v == null) return;
        _imagen = false;
        _exportando = true; _temaClaro = _temaImagen == "claro"; _conTitulo = _conTituloImagen;
        ActualizarResaltado();
        StateHasChanged();
        await Task.Delay(60);
        await _v.InvokeVoidAsync("ajustarTextos");
        var fondo = _transparente && _formatoImagen == "png" ? null : _temaClaro ? "#ffffff" : "#0e1016";
        var error = await _v.InvokeAsync<string?>("exportarImagen", Enlaces.ArchivoPara(_a.Nombre)[..^5] + "." + _formatoImagen, _escalaImagen, _formatoImagen, fondo);
        _exportando = false; _temaClaro = false; _conTitulo = false;
        ActualizarResaltado();
        if (error != null) Toast(error);
        StateHasChanged();
    }

    private bool _conTituloImagen = true;

    private async Task Imprimir()
    {
        if (_v == null) return;
        _imprimir = false;
        _exportando = true; _temaClaro = _temaImpresion == "claro"; _conTitulo = _conTituloImagen;
        ActualizarResaltado();
        StateHasChanged();
        await Task.Delay(60);
        await _v.InvokeVoidAsync("ajustarTextos");
        await _v.InvokeVoidAsync("imprimir", _apaisado);
        await Task.Delay(1500);
        _exportando = false; _temaClaro = false; _conTitulo = false;
        ActualizarResaltado();
        StateHasChanged();
    }

    private string _temaImpresion = "claro";

    private void Renombrar()
    {
        if (_a == null) return;
        var n = _nombreNuevo.Trim();
        _renombrar = false;
        if (n.Length == 0 || n == _a.Nombre) return;
        Instantanea();
        _a.Nombre = n;
        Cambiado();
    }

    public async ValueTask DisposeAsync()
    {
        _temporizador?.Dispose();
        _tToast?.Dispose();
        await GuardarAhora();
        try
        {
            if (!Datos.Local && Datos.Listo) await Datos.Desuscribir();
            if (_v != null) { await _v.InvokeVoidAsync("destruir"); await _v.InvokeVoidAsync("avisarSalida", false); await _v.DisposeAsync(); }
        }
        catch (JSDisconnectedException) { }
        _ref?.Dispose();
    }
}
