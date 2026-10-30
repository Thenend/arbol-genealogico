using System.Text.Json;

namespace ArbolGenealogico.Movil;

/// <summary>Lo que se recuerda de una vez para otra (la forma de las tarjetas y la colocación del árbol).</summary>
public sealed class Preferencias
{
    public string Forma { get; set; } = "horizontal foto";
    public string Ordenacion { get; set; } = "A";
    /// <summary>Marcar en las tarjetas a quien tiene algo escrito en su historia.</summary>
    public bool MarcaHistoria { get; set; } = true;
    /// <summary>Los colores de la aplicación y del árbol (ver <see cref="Dibujo.Tema"/>): «jardin» (la primera vez; «album» en la variante con Álbum), «sencillo», «album», «tinta», «pergamino»…</summary>
    public string Tema { get; set; } = Dibujo.Tema.PorDefecto.Id;
    /// <summary>La letra de las tarjetas (ver <see cref="Dibujo.Letra"/>): «barlow», «lora»…; null hasta elegirla: la del tema.</summary>
    public string? Letra { get; set; }
    /// <summary>Al imprimir: el papel («A4», «Letter»…; null hasta elegirlo: el del país), las hojas y los colores.</summary>
    public string? PapelImpresion { get; set; }
    public int HojasImpresion { get; set; } = 1;
    public string EstiloImpresion { get; set; } = "Claro";
    /// <summary>Al imprimir en varias hojas, cortar a partes iguales aunque se parta alguna tarjeta.</summary>
    public bool CortesIguales { get; set; }
    /// <summary>Al imprimir, sin destacar a la persona principal (un árbol de toda la familia: sin su línea directa en dorado).</summary>
    public bool SinDestacarImpresion { get; set; }
    /// <summary>
    /// Al imprimir, la lámina para regalar (una hoja con título y marco) en vez de las hojas para pegar; su estilo, su
    /// dedicatoria y el título que se le puso al árbol <see cref="ArbolLamina"/> (para otro árbol, se empieza con su nombre).
    /// </summary>
    public bool Lamina { get; set; }
    public string EstiloLamina { get; set; } = "Clasico";
    /// <summary>El estilo del árbol y la letra con los que se imprimió la última vez (ver <see cref="Dibujo.Tema"/> y <see cref="Dibujo.Letra"/>).</summary>
    public string? TemaImpresion { get; set; }
    public string? LetraImpresion { get; set; }
    public string DedicatoriaLamina { get; set; } = "";
    public string? TituloLamina { get; set; }
    public string? ArbolLamina { get; set; }
    /// <summary>
    /// Si la lámina de <see cref="ArbolLamina"/> lleva la foto de fondo (guardada en la carpeta de datos) y con qué
    /// intensidad sale la foto (de 0 a 1; la misma para todos los árboles).
    /// </summary>
    public bool FondoLamina { get; set; }
    public double OpacidadFondoLamina { get; set; } = 0.25;
    /// <summary>Copia automática de los árboles en Documentos (para recuperarlos si se borra la aplicación).</summary>
    public bool CopiaEnDocumentos { get; set; } = true;
    /// <summary>
    /// Cuándo se guardó (o se recuperó) la última copia completa, la de todos los árboles en un archivo fuera del teléfono;
    /// desde cuándo hay cambios que no están en ella (null: ninguno); si se quiere el recordatorio de hacerla y hasta cuándo
    /// no sale (al tocar «Ahora no»).
    /// </summary>
    public DateTime? UltimaCopiaCompleta { get; set; }
    public DateTime? CambiosSinCopiaDesde { get; set; }
    public bool RecordarCopia { get; set; } = true;
    public DateTime? RecordatorioCopiaHasta { get; set; }
    /// <summary>
    /// Si se compró la versión completa, según la última vez que contestó la tienda (para no depender de la conexión: se
    /// vuelve a preguntar al abrir la aplicación).
    /// </summary>
    public bool Completa { get; set; }

    // ---------- Avisos de cumpleaños (ver Avisos) ----------

    public bool AvisosCumple { get; set; } = true;
    /// <summary>La hora del aviso de cada día (de 0 a 23).</summary>
    public int HoraAvisos { get; set; } = 10;
    /// <summary>Además del mismo día, avisar también estos días antes (0: solo ese día).</summary>
    public int DiasAntesAvisos { get; set; }
    /// <summary>Avisar también de los aniversarios de las muertes.</summary>
    public bool AvisosAniversarios { get; set; }
    /// <summary>Avisar también de los aniversarios de boda (de las parejas que siguen juntas).</summary>
    public bool AvisosBodas { get; set; } = true;
    /// <summary>Los árboles (el nombre de su archivo) de los que no se quiere aviso; de los demás, sí.</summary>
    public List<string> ArbolesSinAvisos { get; set; } = new();
    /// <summary>Si ya se preguntó si se quieren los avisos (se pregunta una sola vez).</summary>
    public bool PermisoAvisosPreguntado { get; set; }
    /// <summary>El último día que se avisó («2026-10-26»), para no avisar dos veces el mismo día.</summary>
    public string? UltimoAviso { get; set; }

    // ---------- En el ordenador ----------

    /// <summary>El último árbol abierto (se vuelve a abrir al empezar).</summary>
    public string? UltimoArchivo { get; set; }
    /// <summary>Dónde estaba la ventana y si estaba maximizada.</summary>
    public double[]? Ventana { get; set; }
    public bool VentanaMaximizada { get; set; }
    /// <summary>Los árboles propios (creados con «Nuevo» o importados de GEDCOM), para la versión gratuita.</summary>
    public List<string> PropiosPc { get; set; } = new();

    private static string Ruta => Path.Combine(App.Plataforma.CarpetaDatos, "preferencias.json");
    private static Preferencias? _actual;

    public static Preferencias Actual
    {
        get
        {
            if (_actual != null) return _actual;
            try { _actual = File.Exists(Ruta) ? JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) : null; }
            catch { _actual = null; }
            return _actual ??= new Preferencias();
        }
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
            File.WriteAllText(Ruta, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
