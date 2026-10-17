using System.Security.Cryptography;
using System.Text;

namespace ArbolGenealogico.Core.Model;

/// <summary>
/// Identificadores únicos globales (32 cifras hexadecimales), para que las personas de dos archivos distintos no
/// se confundan y se puedan fusionar árboles que se manda la familia. Los archivos de la versión 1 usaban ids
/// secuenciales (p1, u1…) que se repiten entre árboles: al abrirlos se convierten.
/// </summary>
public sealed partial class Arbol
{
    public const int VersionActual = 2;

    public static string NuevoId() => Guid.NewGuid().ToString("N");

    public static bool EsIdGlobal(string id) =>
        id.Length == 32 && id.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>
    /// Cambia los ids que no son globales por ids globales y actualiza todas las referencias. Es determinista:
    /// dos copias idénticas de un mismo archivo antiguo dan los mismos ids, y así se reconocen al fusionarlas.
    /// Devuelve si cambió algo.
    /// </summary>
    public bool MigrarIds(string huellaContenido)
    {
        bool cambia = Personas.Any(p => !EsIdGlobal(p.Id)) || Uniones.Any(u => !EsIdGlobal(u.Id));
        if (cambia)
        {
            var semilla = SHA256.HashData(Encoding.UTF8.GetBytes(huellaContenido));
            string Derivar(string tipo, string id)
            {
                var datos = semilla.Concat(Encoding.UTF8.GetBytes(tipo + ":" + id)).ToArray();
                return Convert.ToHexStringLower(SHA256.HashData(datos), 0, 16);
            }
            var mp = Personas.ToDictionary(p => p.Id, p => EsIdGlobal(p.Id) ? p.Id : Derivar("p", p.Id));
            foreach (var p in Personas) p.Id = mp[p.Id];
            foreach (var u in Uniones)
            {
                if (!EsIdGlobal(u.Id)) u.Id = Derivar("u", u.Id);
                u.Parejas = u.Parejas.Select(x => mp[x]).ToList();
                u.Hijos = u.Hijos.Select(x => mp[x]).ToList();
            }
            if (mp.TryGetValue(RaizId, out var raiz)) RaizId = raiz;
        }
        if (Version < VersionActual) { Version = VersionActual; cambia = true; }
        return cambia;
    }
}
