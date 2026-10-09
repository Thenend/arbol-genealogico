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
