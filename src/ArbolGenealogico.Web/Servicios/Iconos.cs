using Microsoft.AspNetCore.Components;

namespace ArbolGenealogico.Web.Servicios;

/// <summary>Iconos de línea (SVG en línea, sin fuentes de iconos ni descargas).</summary>
public static class Iconos
{
    private static MarkupString S(string d) => new($"<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\">{d}</svg>");

    public static readonly MarkupString Atras = S("<path d=\"M15 18l-6-6 6-6\"/>");
    public static readonly MarkupString Mas = S("<path d=\"M12 5v14M5 12h14\"/>");
    public static readonly MarkupString Menos = S("<path d=\"M5 12h14\"/>");
    public static readonly MarkupString Buscar = S("<circle cx=\"11\" cy=\"11\" r=\"7\"/><path d=\"M20 20l-3.5-3.5\"/>");
    public static readonly MarkupString Cerrar = S("<path d=\"M6 6l12 12M18 6L6 18\"/>");
    public static readonly MarkupString Puntos = S("<circle cx=\"5\" cy=\"12\" r=\"1.3\"/><circle cx=\"12\" cy=\"12\" r=\"1.3\"/><circle cx=\"19\" cy=\"12\" r=\"1.3\"/>");
    public static readonly MarkupString Compartir = S("<circle cx=\"9\" cy=\"8\" r=\"3.5\"/><path d=\"M2.5 20c.6-3.6 3.2-5.5 6.5-5.5s5.9 1.9 6.5 5.5\"/><path d=\"M19 8v6M16 11h6\"/>");
    public static readonly MarkupString Deshacer = S("<path d=\"M9 14L4 9l5-5\"/><path d=\"M4 9h10.5a5.5 5.5 0 010 11H11\"/>");
    public static readonly MarkupString Rehacer = S("<path d=\"M15 14l5-5-5-5\"/><path d=\"M20 9H9.5a5.5 5.5 0 000 11H13\"/>");
    public static readonly MarkupString Tarjeta = S("<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"3\"/><circle cx=\"8.5\" cy=\"12\" r=\"2.5\"/><path d=\"M13.5 10.5h4M13.5 13.5h3\"/>");
    public static readonly MarkupString Imagen = S("<rect x=\"3\" y=\"4\" width=\"18\" height=\"16\" rx=\"2.5\"/><circle cx=\"9\" cy=\"10\" r=\"2\"/><path d=\"M21 16l-5-5-9 9\"/>");
    public static readonly MarkupString Imprimir = S("<path d=\"M6 9V3h12v6\"/><rect x=\"3\" y=\"9\" width=\"18\" height=\"8\" rx=\"2\"/><path d=\"M6 14h12v7H6z\"/>");
    public static readonly MarkupString Descargar = S("<path d=\"M12 4v11M7 10l5 5 5-5\"/><path d=\"M4 20h16\"/>");
    public static readonly MarkupString Subir = S("<path d=\"M12 20V9M7 14l5-5 5 5\"/><path d=\"M4 4h16\"/>");
    public static readonly MarkupString Editar = S("<path d=\"M4 20h4L19 9l-4-4L4 16z\"/><path d=\"M13.5 6.5l4 4\"/>");
    public static readonly MarkupString Borrar = S("<path d=\"M4 7h16M10 11v6M14 11v6\"/><path d=\"M6 7l1 13h10l1-13M9 7V4h6v3\"/>");
    public static readonly MarkupString Enlace = S("<path d=\"M10 14a4 4 0 005.7 0l3-3a4 4 0 00-5.7-5.7l-1 1\"/><path d=\"M14 10a4 4 0 00-5.7 0l-3 3a4 4 0 005.7 5.7l1-1\"/>");
    public static readonly MarkupString Historia = S("<path d=\"M6 3h9l4 4v14H6z\"/><path d=\"M9 11h7M9 15h7\"/>");
    public static readonly MarkupString Padres = S("<path d=\"M12 20v-7M12 13H6V8M12 13h6V8\"/><circle cx=\"6\" cy=\"5\" r=\"2\"/><circle cx=\"18\" cy=\"5\" r=\"2\"/>");
    public static readonly MarkupString Hermano = S("<circle cx=\"7\" cy=\"8\" r=\"3\"/><circle cx=\"17\" cy=\"8\" r=\"3\"/><path d=\"M2 20c.5-3.5 2.4-5 5-5s4.5 1.5 5 5M12 20c.5-3.5 2.4-5 5-5s4.5 1.5 5 5\"/>");
    public static readonly MarkupString Pareja = S("<path d=\"M12 20s-7-4.4-7-10a4 4 0 017-2.6A4 4 0 0119 10c0 5.6-7 10-7 10z\"/>");
    public static readonly MarkupString Hijo = S("<path d=\"M12 4v8M12 12H6v4M12 12h6v4\"/><circle cx=\"6\" cy=\"19\" r=\"2\"/><circle cx=\"18\" cy=\"19\" r=\"2\"/>");
    public static readonly MarkupString Arbol = S("<path d=\"M12 21v-6M12 15l-5-4M12 15l5-4\"/><circle cx=\"12\" cy=\"5\" r=\"2.5\"/><circle cx=\"6\" cy=\"10\" r=\"2.5\"/><circle cx=\"18\" cy=\"10\" r=\"2.5\"/>");
    public static readonly MarkupString Principal = S("<path d=\"M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z\"/>");
    public static readonly MarkupString Ajustar = S("<path d=\"M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5\"/>");
    public static readonly MarkupString Casa = S("<path d=\"M4 11l8-7 8 7v9H4z\"/><path d=\"M10 20v-5h4v5\"/>");
    public static readonly MarkupString Salir = S("<path d=\"M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10\"/>");
    public static readonly MarkupString Teclado = S("<rect x=\"2\" y=\"6\" width=\"20\" height=\"12\" rx=\"2\"/><path d=\"M6 10h.01M10 10h.01M14 10h.01M18 10h.01M7 14h10\"/>");
    public static readonly MarkupString Colocacion = S("<rect x=\"3\" y=\"3\" width=\"7\" height=\"5\" rx=\"1\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"5\" rx=\"1\"/><rect x=\"8.5\" y=\"16\" width=\"7\" height=\"5\" rx=\"1\"/><path d=\"M6.5 8v4h11V8M12 12v4\"/>");
    public static readonly MarkupString Llave = S("<circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"M11 12l8-8M16 7l2 2M14 9l2 2\"/>");
    public static readonly MarkupString Nube = S("<path d=\"M7 18a5 5 0 01-.6-9.95A6 6 0 0118 9a4.5 4.5 0 01-.5 9z\"/>");
}
