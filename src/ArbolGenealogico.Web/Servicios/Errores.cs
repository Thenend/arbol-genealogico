namespace ArbolGenealogico.Web.Servicios;

public static class Errores
{
    /// <summary>El mensaje para enseñar: solo la primera línea (los errores de JavaScript traen detrás la traza).</summary>
    public static string Texto(this Exception e)
    {
        var m = e.Message.Trim();
        int salto = m.IndexOf('\n');
        return salto < 0 ? m : m[..salto].TrimEnd();
    }
}

public static class Listas
{
    /// <summary>El único elemento, o null si no hay ninguno o hay más de uno.</summary>
    public static T? SingleOrDefaultOrNull<T>(this IEnumerable<T> xs) where T : class
    {
        var l = xs.Take(2).ToList();
        return l.Count == 1 ? l[0] : null;
    }
}
