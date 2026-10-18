using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArbolGenealogico.Core.Model;

namespace ArbolGenealogico.Core.Io;

/// <summary>
/// GEDCOM, el formato de intercambio de los programas de genealogía (Ancestry, MyHeritage, FamilySearch, Geneanet…).
/// Se escribe GEDCOM 5.5.1 en UTF-8: personas (nombre, apellidos, sexo, historia como nota) y familias (pareja e hijos),
/// sin fotos. Se lee cualquier 5.x (UTF-8, UTF-16, ANSEL o Windows-1252): lo que este programa no guarda como dato
/// (fechas y lugares de nacimiento, bautizo, boda, defunción…, oficios, residencias) se escribe al principio de la
/// historia de cada persona en frases normales, y detrás van sus notas.
/// </summary>
public static class Gedcom
{
    public const string Extension = ".ged";

    public static bool EsGedcom(string nombre) =>
        nombre.EndsWith(".ged", StringComparison.OrdinalIgnoreCase) || nombre.EndsWith(".gedcom", StringComparison.OrdinalIgnoreCase);

    /// <summary>Si el contenido parece un GEDCOM (empieza por «0 HEAD»), venga con el nombre que venga.</summary>
    public static bool PareceGedcom(byte[] datos)
    {
        try
        {
            var inicio = Decodificar(datos.Length > 4096 ? datos[..4096] : datos).TrimStart('﻿', ' ', '\t', '\r', '\n');
            return inicio.StartsWith("0 HEAD", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // =====================================================================================================
    // Escribir
    // =====================================================================================================

    /// <summary>El árbol en GEDCOM 5.5.1, en UTF-8 con BOM (como lo esperan los programas de genealogía).</summary>
    public static byte[] Exportar(Arbol a, string? nombreArchivo = null) =>
        new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(ExportarTexto(a, nombreArchivo))).ToArray();

    public static string ExportarTexto(Arbol a, string? nombreArchivo = null)
    {
        var sb = new StringBuilder();
        void L(int nivel, string resto) => sb.Append(nivel).Append(' ').Append(resto).Append("\r\n");

        // la persona principal, la primera: muchos programas abren el árbol por la primera persona
        var personas = a.Personas.OrderBy(p => p.Id == a.RaizId ? 0 : 1).ToList();
        var xi = new Dictionary<string, string>();
        for (int i = 0; i < personas.Count; i++) xi[personas[i].Id] = $"@I{i + 1}@";
        var uniones = a.Uniones.Where(u => u.Parejas.Concat(u.Hijos).Any(xi.ContainsKey)).ToList();
        var xf = new Dictionary<Union, string>();
        for (int i = 0; i < uniones.Count; i++) xf[uniones[i]] = $"@F{i + 1}@";

        L(0, "HEAD");
        L(1, "SOUR RAMO");
        L(2, "NAME Ramo");
        L(1, "DEST ANY");
        L(1, "DATE " + FechaGedcom(DateTime.Now));
        L(1, "SUBM @U1@");
        if (!string.IsNullOrWhiteSpace(nombreArchivo)) L(1, "FILE " + Escapar(nombreArchivo!));
        L(1, "GEDC");
        L(2, "VERS 5.5.1");
        L(2, "FORM LINEAGE-LINKED");
        L(1, "CHAR UTF-8");
        if (!string.IsNullOrWhiteSpace(a.Nombre)) Texto(sb, 1, "NOTE", "Árbol «" + a.Nombre.Trim() + "», exportado desde Ramo.");
        L(0, "@U1@ SUBM");
        L(1, "NAME Ramo");

        foreach (var p in personas)
        {
            L(0, xi[p.Id] + " INDI");
            string nombre = p.Nombre.Trim(), apellidos = p.Apellidos.Trim();
            L(1, "NAME " + Escapar((nombre + " /" + apellidos + "/").Trim()));
            if (nombre.Length > 0) L(2, "GIVN " + Escapar(nombre));
            if (apellidos.Length > 0) L(2, "SURN " + Escapar(apellidos));
            L(1, "SEX " + (p.Sexo switch { Sexo.Hombre => "M", Sexo.Mujer => "F", _ => "U" }));
            if (!string.IsNullOrWhiteSpace(p.Historia)) Texto(sb, 1, "NOTE", p.Historia.Trim());
            // el id del árbol: si el archivo vuelve a este programa, las personas se reconocen (y se puede fusionar)
            if (Arbol.EsIdGlobal(p.Id)) L(1, "_UID " + p.Id.ToUpperInvariant());
            foreach (var u in uniones.Where(u => u.Hijos.Contains(p.Id))) L(1, "FAMC " + xf[u]);
            foreach (var u in uniones.Where(u => u.Parejas.Contains(p.Id))) L(1, "FAMS " + xf[u]);
        }

        foreach (var u in uniones)
        {
            L(0, xf[u] + " FAM");
            // GEDCOM 5.5.1 distingue marido y mujer: el hombre como HUSB y la mujer como WIFE; si no se sabe, por orden
            var ps = u.Parejas.Where(xi.ContainsKey).Select(id => a.Buscar(id)!).ToList();
            Persona? husb = null, wife = null;
            foreach (var q in ps.Where(q => q.Sexo == Sexo.Hombre)) husb ??= q;
            foreach (var q in ps.Where(q => q.Sexo == Sexo.Mujer)) wife ??= q;
            foreach (var q in ps.Where(q => q != husb && q != wife))
                if (husb == null) husb = q; else wife ??= q;
            if (husb != null) L(1, "HUSB " + xi[husb.Id]);
            if (wife != null) L(1, "WIFE " + xi[wife.Id]);
            foreach (var h in u.Hijos.Where(xi.ContainsKey)) L(1, "CHIL " + xi[h]);
        }
        L(0, "TRLR");
        return sb.ToString();
    }

    private static string Escapar(string s) => s.Replace("@", "@@").Replace("\r", " ").Replace("\n", " ");

    private static readonly string[] Meses = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

    private static string FechaGedcom(DateTime d) => $"{d.Day} {Meses[d.Month - 1]} {d.Year}";

    /// <summary>
    /// Un texto largo (una nota): cada línea en un CONT y las líneas largas partidas en CONC, sin cortar junto a un espacio
    /// (hay programas que quitan los espacios del principio o del final de cada trozo).
    /// </summary>
    private static void Texto(StringBuilder sb, int nivel, string etiqueta, string texto)
    {
        const int Max = 200;
        var lineas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lineas.Length; i++)
        {
            var resto = Escapar(lineas[i]);
            bool primero = true;
            do
            {
                int corte = resto.Length;
                if (resto.Length > Max)
                {
                    corte = Max;
                    while (corte > Max / 2 && (resto[corte - 1] == ' ' || resto[corte] == ' ')) corte--;
                    // no se parte un «@@» (sería una arroba suelta)
                    if (resto[corte - 1] == '@' && resto[corte] == '@') corte--;
                }
                var trozo = resto[..corte];
                resto = resto[corte..];
                string tag = i == 0 && primero ? etiqueta : primero ? "CONT" : "CONC";
                int n = i == 0 && primero ? nivel : nivel + 1;
                sb.Append(n).Append(' ').Append(tag);
                if (trozo.Length > 0) sb.Append(' ').Append(trozo);
                sb.Append("\r\n");
                primero = false;
            } while (resto.Length > 0);
        }
    }

    // =====================================================================================================
    // Leer
    // =====================================================================================================

    private sealed class Nodo
    {
        public string? Xref;
        public string Tag = "";
        public string Valor = "";
        public readonly List<Nodo> Hijos = new();

        public Nodo? Uno(string tag) => Hijos.FirstOrDefault(h => h.Tag == tag);
        public IEnumerable<Nodo> Todos(string tag) => Hijos.Where(h => h.Tag == tag);
        public string Dato(string tag) => Uno(tag)?.Valor.Trim() ?? "";
    }

    private static readonly Regex RegLinea = new(@"^\s*(\d+)\s+(?:(@[^@\s]+@)\s+)?([A-Za-z0-9_]+)(?: (.*))?$", RegexOptions.Compiled);

    /// <summary>
    /// Lee un GEDCOM. <paramref name="nombre"/> es el nombre del archivo (da nombre al árbol). Los ids de las personas son
    /// siempre los mismos para el mismo archivo (o los de Ramo, si vienen en _UID), así que importarlo dos veces y fusionar
    /// no duplica a nadie.
    /// </summary>
    public static Arbol Importar(byte[] datos, string nombre, out List<string> avisos)
    {
        avisos = new List<string>();
        var texto = Decodificar(datos);
        var raices = Leer(texto);
        if (raices.Count == 0 || raices[0].Tag != "HEAD") throw new InvalidDataException("No es un archivo GEDCOM.");

        var huella = Convert.ToHexString(SHA256.HashData(datos));
        string IdDe(string xref) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(huella + "|" + xref)))[..32].ToLowerInvariant();

        var notas = raices.Where(r => r.Tag == "NOTE" && r.Xref != null).GroupBy(r => r.Xref!).ToDictionary(g => g.Key, g => g.First());
        var indis = raices.Where(r => r.Tag == "INDI" && r.Xref != null).ToList();
        var fams = raices.Where(r => r.Tag == "FAM" && r.Xref != null).ToList();
        if (indis.Count == 0) throw new InvalidDataException("El archivo GEDCOM no tiene ninguna persona.");

        var a = new Arbol { Nombre = string.IsNullOrWhiteSpace(nombre) ? "Árbol importado" : Path.GetFileNameWithoutExtension(nombre) };
        // si lo exportó este programa, el nombre del árbol va en una nota de la cabecera
        if (Regex.Match(raices[0].Dato("NOTE"), @"^Árbol «(.+)», exportado desde Ramo\.$") is { Success: true } mn) a.Nombre = mn.Groups[1].Value;
        var porXref = new Dictionary<string, Persona>();
        var usados = new HashSet<string>();
        // de cada persona: lo de su vida (nacimiento, oficio, bodas…), lo del final (muerte, entierro) y las notas
        var historias = new Dictionary<Persona, (List<string> Frases, List<string> Final, List<string> Notas)>();
        int conFotos = 0;
        foreach (var n in indis)
        {
            if (porXref.ContainsKey(n.Xref!)) continue;
            var p = new Persona();
            // el id: el de Ramo si lo trae (_UID), y si no uno fijo para este archivo y esta persona
            var uid = new string((n.Dato("_UID") is { Length: > 0 } u ? u : n.Dato("UID")).Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
            if (uid.Length >= 32 && Arbol.EsIdGlobal(uid[..32]) && !usados.Contains(uid[..32])) p.Id = uid[..32];
            else p.Id = IdDe(n.Xref!);
            usados.Add(p.Id);
            (p.Nombre, p.Apellidos) = Nombre(n);
            p.Sexo = n.Dato("SEX").ToUpperInvariant() switch { "M" => Sexo.Hombre, "F" => Sexo.Mujer, _ => Sexo.Desconocido };
            if (n.Uno("OBJE") != null) conFotos++;
            porXref[n.Xref!] = p;
            a.Personas.Add(p);
            var (vida, final) = Frases(n, p);
            historias[p] = (vida, final, NotasDe(n, notas));
        }

        foreach (var f in fams)
        {
            var parejas = f.Todos("HUSB").Concat(f.Todos("WIFE")).Select(x => porXref.GetValueOrDefault(x.Valor.Trim())).OfType<Persona>().Distinct().ToList();
            var hijos = f.Todos("CHIL").Select(x => porXref.GetValueOrDefault(x.Valor.Trim())).OfType<Persona>().Distinct().ToList();
            // unos hermanos de padres desconocidos: un padre «sin nombre» los mantiene juntos (si no, quedarían sueltos)
            if (parejas.Count == 0 && hijos.Count >= 2)
            {
                var desconocido = new Persona { Id = IdDe(f.Xref! + "/padre") };
                a.Personas.Add(desconocido);
                historias[desconocido] = (new(), new(), new());
                parejas.Add(desconocido);
            }
            if (parejas.Count == 0) continue;
            var ids = parejas.Take(2).Select(q => q.Id).ToList();
            // la misma pareja en varias familias (p. ej. se casaron dos veces): una sola unión con los hijos de todas
            var u = a.Uniones.FirstOrDefault(x => x.Parejas.Count == ids.Count && !x.Parejas.Except(ids).Any());
            if (u == null) a.Uniones.Add(u = new Union { Id = IdDe(f.Xref!), Parejas = ids });
            foreach (var h in hijos) if (!u.Hijos.Contains(h.Id)) u.Hijos.Add(h.Id);
            // la boda y el divorcio, en la historia de los dos
            if (parejas.Count == 2)
                for (int i = 0; i < 2; i++)
                {
                    var yo = parejas[i]; var otro = parejas[1 - i];
                    string quien = otro.NombreCompleto.Length > 0 ? otro.NombreCompleto : "su pareja";
                    if (f.Uno("MARR") is { } marr && Suceso(marr) is { } bodas) historias[yo].Frases.Add($"Se casó con {quien}{bodas}.");
                    if (f.Uno("DIV") is { } div && Suceso(div) is { } divorcio) historias[yo].Frases.Add($"Se divorció de {quien}{divorcio}.");
                }
            foreach (var nota in NotasDe(f, notas))
                foreach (var q in parejas) historias[q].Notas.Add(nota);
        }

        foreach (var (p, (frases, final, ns)) in historias)
        {
            var partes = new List<string>();
            if (frases.Count + final.Count > 0) partes.Add(string.Join(" ", frases.Concat(final)));
            partes.AddRange(ns.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct());
            p.Historia = string.Join("\n\n", partes);
        }

        // la persona principal: la primera del grupo más grande de personas unidas entre sí (en un GEDCOM puede haber
        // varias familias sueltas; se ve la mayor)
        a.RaizId = PrincipalDe(a);
        avisos.AddRange(a.Reparar());
        if (conFotos > 0)
            avisos.Add(conFotos == 1 ? "Una persona tenía foto en el archivo: las fotos de GEDCOM no se importan."
                                      : $"{conFotos} personas tenían foto en el archivo: las fotos de GEDCOM no se importan.");
        return a;
    }

    /// <summary>Las líneas del archivo, como árbol de nodos (CONT y CONC ya unidos al texto de su nodo).</summary>
    private static List<Nodo> Leer(string texto)
    {
        var raices = new List<Nodo>();
        var pila = new List<Nodo>();
        foreach (var bruta in texto.Split('\n'))
        {
            var linea = bruta.TrimEnd('\r').TrimStart('﻿');
            if (linea.Trim().Length == 0) continue;
            var m = RegLinea.Match(linea);
            if (!m.Success) continue;
            int nivel = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            string tag = m.Groups[3].Value.ToUpperInvariant();
            string valor = m.Groups[4].Success ? m.Groups[4].Value : "";
            if (!valor.StartsWith('@') || !valor.EndsWith('@') || valor.Contains(' ')) valor = valor.Replace("@@", "@");
            if ((tag == "CONT" || tag == "CONC") && nivel > 0 && pila.Count >= nivel)
            {
                var padre = pila[nivel - 1];
                padre.Valor += (tag == "CONT" ? "\n" : "") + valor;
                continue;
            }
            var n = new Nodo { Xref = m.Groups[2].Success ? m.Groups[2].Value : null, Tag = tag, Valor = valor };
            if (nivel == 0) raices.Add(n);
            else if (pila.Count >= nivel) pila[nivel - 1].Hijos.Add(n);
            else continue;      // nivel que salta: línea mal formada
            if (pila.Count > nivel) pila.RemoveRange(nivel, pila.Count - nivel);
            pila.Add(n);
        }
        return raices;
    }

    /// <summary>Nombre y apellidos: «Juan /Pérez García/» o «Juan /Pérez/ García» (lo de después de la barra, también apellido).</summary>
    private static (string Nombre, string Apellidos) Nombre(Nodo indi)
    {
        var n = indi.Uno("NAME");
        if (n == null) return ("", "");
        var v = n.Valor.Trim();
        string nombre, apellidos;
        int i = v.IndexOf('/');
        if (i >= 0)
        {
            int j = v.IndexOf('/', i + 1);
            nombre = v[..i];
            apellidos = j > i ? v[(i + 1)..j] + " " + v[(j + 1)..] : v[(i + 1)..];
        }
        else { nombre = v; apellidos = ""; }
        if (nombre.Trim().Length == 0 && n.Dato("GIVN") is { Length: > 0 } givn) nombre = givn;
        if (apellidos.Trim().Length == 0 && n.Dato("SURN") is { Length: > 0 } surn) apellidos = surn;
        static string Limpio(string s)
        {
            s = Regex.Replace(s, @"\s+", " ").Trim();
            // muchos programas escriben los apellidos en mayúsculas («PÉREZ»): se ponen como se escriben normalmente
            // (palabra a palabra: «PÉREZ García» → «Pérez García»)
            return Regex.Replace(s, @"\p{L}+", m => m.Value.Length > 1 && m.Value.All(char.IsUpper)
                ? m.Value[0] + m.Value[1..].ToLowerInvariant() : m.Value);
        }
        return (Limpio(nombre), Limpio(apellidos));
    }

    private static List<string> NotasDe(Nodo n, Dictionary<string, Nodo> notas)
    {
        var lista = new List<string>();
        foreach (var nota in n.Todos("NOTE"))
        {
            var v = nota.Valor.Trim();
            if (v.StartsWith('@') && v.EndsWith('@') && !v.Contains(' '))
            {
                if (notas.TryGetValue(v, out var registro)) lista.Add(registro.Valor.Trim());
            }
            else if (v.Length > 0) lista.Add(nota.Valor.Trim());
        }
        return lista;
    }

    /// <summary>
    /// Las frases con los datos de la persona que este programa no guarda aparte, en el orden de una vida: lo de su vida
    /// (y detrás irán las bodas) y, aparte, lo del final (muerte y entierro), que va lo último.
    /// </summary>
    private static (List<string> Vida, List<string> Final) Frases(Nodo n, Persona p)
    {
        bool f = p.Sexo == Sexo.Mujer;
        string A(string masculino) => f ? masculino[..^1] + "a" : masculino;
        var frases = new List<string>();
        var final = new List<string>();
        void Evento(string tag, string verbo, bool sinNadaTambien = false)
        {
            foreach (var e in n.Todos(tag))
            {
                if (Suceso(e) is { } detalle) frases.Add(verbo + detalle + ".");
                else if (sinNadaTambien && e.Valor.Trim().ToUpperInvariant() == "Y") frases.Add(verbo + ".");
            }
        }
        void Dato(string tag, string etiqueta)
        {
            foreach (var e in n.Todos(tag))
            {
                var v = e.Valor.Trim();
                var detalle = Suceso(e, conValor: false) ?? "";
                if (v.Length > 0) frases.Add($"{etiqueta}: {v}{detalle}.");
                else if (detalle.Length > 0) frases.Add($"{etiqueta}:{detalle}.");
            }
        }
        Evento("BIRT", "Nació");
        Evento("CHR", "Fue " + A("bautizado"));
        Evento("BAPM", "Fue " + A("bautizado"));
        Evento("ADOP", "Fue " + A("adoptado"));
        Dato("OCCU", "Oficio");
        Dato("EDUC", "Estudios");
        Dato("TITL", "Título");
        Dato("RELI", "Religión");
        Dato("NATI", "Origen");
        foreach (var e in n.Todos("RESI"))
            if (Suceso(e) is { } d) frases.Add("Vivió" + (e.Valor.Trim().Length > 0 ? " en " + e.Valor.Trim() : "") + d + ".");
        // el lugar de la emigración unos programas lo usan como origen y otros como destino: va entre paréntesis
        foreach (var e in n.Todos("EMIG"))
            if (Suceso(e, lugarAparte: true) is { } d) frases.Add("Emigró" + d + ".");
        foreach (var e in n.Todos("IMMI"))
        {
            var lugar = e.Dato("PLAC");
            var fecha = FraseFecha(e.Dato("DATE"));
            if (lugar.Length > 0) frases.Add("Llegó a " + lugar + (fecha.Length > 0 ? " " + fecha : "") + ".");
            else if (fecha.Length > 0) frases.Add("Inmigró " + fecha + ".");
        }
        Evento("NATU", "Se nacionalizó");
        Evento("GRAD", "Se graduó");
        Evento("RETI", "Se jubiló");
        foreach (var e in n.Todos("EVEN").Concat(n.Todos("FACT")))
        {
            var tipo = e.Dato("TYPE");
            var v = e.Valor.Trim();
            var d = Suceso(e, conValor: false) ?? "";
            var titulo = tipo.Length > 0 ? tipo : "Otro dato";
            if (v.Length > 0 || d.Length > 0) frases.Add($"{titulo}: {v}{d}".Replace(": ,", ":") + ".");
        }
        var vida = frases;
        frases = final;
        Evento("DEAT", "Murió", sinNadaTambien: true);
        Evento("BURI", "Fue " + A("enterrado"));
        Evento("CREM", "Fue " + A("incinerado"));
        return (vida, final);
    }

    /// <summary>La fecha y el lugar de un suceso como final de frase (« el 3 de marzo de 1920 en Ávila»), o null si no hay.</summary>
    private static string? Suceso(Nodo e, bool conValor = true, bool lugarAparte = false)
    {
        var fecha = FraseFecha(e.Dato("DATE"));
        var lugar = e.Dato("PLAC");
        if (lugar.Length == 0 && e.Uno("ADDR") is { } dir) lugar = dir.Valor.Replace("\n", ", ").Trim();
        var s = (fecha.Length > 0 ? " " + fecha : "") + (lugar.Length == 0 ? "" : lugarAparte ? " (" + lugar + ")" : " en " + lugar);
        return s.Length > 0 ? s : null;
    }

    private static readonly Dictionary<string, string> NombresMes = new()
    {
        ["JAN"] = "enero", ["FEB"] = "febrero", ["MAR"] = "marzo", ["APR"] = "abril", ["MAY"] = "mayo", ["JUN"] = "junio",
        ["JUL"] = "julio", ["AUG"] = "agosto", ["SEP"] = "septiembre", ["OCT"] = "octubre", ["NOV"] = "noviembre", ["DEC"] = "diciembre",
        // calendario republicano francés y hebreo: se dejan como vienen
    };

    /// <summary>Una fecha GEDCOM («3 MAR 1920», «ABT 1900», «BET 1900 AND 1910»…) como parte de una frase en español.</summary>
    public static string FraseFecha(string d)
    {
        d = Regex.Replace(d ?? "", @"@#D[A-Z ]+@\s*", "").Trim();
        if (d.Length == 0) return "";
        var m = Regex.Match(d, @"^\((.*)\)$");
        if (m.Success) return "(" + m.Groups[1].Value.Trim() + ")";
        var partes = d.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string Q(int desde) => string.Join(' ', partes.Skip(desde));
        // lo que no es una fecha GEDCOM («second tuesday of spring») se deja como viene, entre paréntesis
        if (!Regex.IsMatch(d, @"^((ABT|CAL|EST|BEF|AFT|BET|AND|FROM|TO|INT)\s+|\d{1,2}\s+|[A-Z]{3,4}\s+|\d{1,4}(/\d{1,2})?(\s+B\.?C\.?)?\s*|\(.*\)\s*)+$", RegexOptions.IgnoreCase)
            || !partes.Any(x => Regex.IsMatch(x, @"^\d{3,4}(/\d{1,2})?$")))
            return "(" + d + ")";
        switch (partes[0].ToUpperInvariant())
        {
            case "ABT": case "CAL": case "EST": return "hacia " + Simple(Q(1));
            case "BEF": return "antes " + De(Q(1));
            case "AFT": return "después " + De(Q(1));
            case "BET":
            {
                var mm = Regex.Match(Q(1), @"^(.*?)\s+AND\s+(.*)$", RegexOptions.IgnoreCase);
                return mm.Success ? $"entre {Simple(mm.Groups[1].Value)} y {Simple(mm.Groups[2].Value)}" : "hacia " + Simple(Q(1));
            }
            case "FROM":
            {
                var mm = Regex.Match(Q(1), @"^(.*?)\s+TO\s+(.*)$", RegexOptions.IgnoreCase);
                return mm.Success ? $"desde {Simple(mm.Groups[1].Value)} hasta {Simple(mm.Groups[2].Value)}" : "desde " + Simple(Q(1));
            }
            case "TO": return "hasta " + Simple(Q(1));
            case "INT":
            {
                var mm = Regex.Match(Q(1), @"^(.*?)\s*\((.*)\)\s*$");
                return mm.Success ? Frase(mm.Groups[1].Value) + " (" + mm.Groups[2].Value.Trim() + ")" : Frase(Q(1));
            }
        }
        return Frase(d);

        // «el 3 de marzo de 1920», «en marzo de 1920», «en 1920»
        static string Frase(string s)
        {
            var t = Simple(s);
            return char.IsDigit(t.FirstOrDefault()) && t.Contains(" de ") ? "el " + t : "en " + t;
        }
        // «antes del 3 de marzo de 1920», «antes de 1920»
        static string De(string s)
        {
            var t = Simple(s);
            return char.IsDigit(t.FirstOrDefault()) && t.Contains(" de ") ? "del " + t : "de " + t;
        }
    }

    /// <summary>«3 MAR 1920» → «3 de marzo de 1920»; «MAR 1920» → «marzo de 1920»; «1920» → «1920».</summary>
    private static string Simple(string s)
    {
        var p = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string Mes(string x) => NombresMes.TryGetValue(x.ToUpperInvariant(), out var mes) ? mes : x;
        return p.Length switch
        {
            3 when int.TryParse(p[0], out var dia) => $"{dia} de {Mes(p[1])} de {p[2]}",
            2 => $"{Mes(p[0])} de {p[1]}",
            _ => s.Trim(),
        };
    }

    /// <summary>La persona principal al importar: la primera (en el orden del archivo) del grupo más grande de personas unidas.</summary>
    private static string PrincipalDe(Arbol a)
    {
        if (a.Personas.Count == 0) return "";
        var padre = a.Personas.ToDictionary(p => p.Id, p => p.Id);
        string Raiz(string x) { while (padre[x] != x) { padre[x] = padre[padre[x]]; x = padre[x]; } return x; }
        foreach (var u in a.Uniones)
        {
            var ids = u.Parejas.Concat(u.Hijos).Where(padre.ContainsKey).ToList();
            for (int i = 1; i < ids.Count; i++) padre[Raiz(ids[i])] = Raiz(ids[0]);
        }
        var tamanos = a.Personas.GroupBy(p => Raiz(p.Id)).ToDictionary(g => g.Key, g => g.Count());
        int mayor = tamanos.Values.Max();
        return a.Personas.First(p => tamanos[Raiz(p.Id)] == mayor).Id;
    }

    // =====================================================================================================
    // Codificación
    // =====================================================================================================

    /// <summary>
    /// El texto del archivo: con BOM, la que diga; si no, la que declare (1 CHAR): UTF-8, UNICODE (UTF-16), ANSEL o
    /// ANSI/IBMPC (Windows-1252). Si declara ANSEL pero es UTF-8 válido (pasa a menudo), UTF-8.
    /// </summary>
    private static string Decodificar(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return new UTF8Encoding(false).GetString(b, 3, b.Length - 3);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
        // UTF-16 sin BOM: «0» seguido de un cero
        if (b.Length >= 2 && b[0] == (byte)'0' && b[1] == 0) return Encoding.Unicode.GetString(b);
        if (b.Length >= 2 && b[0] == 0 && b[1] == (byte)'0') return Encoding.BigEndianUnicode.GetString(b);

        var cabecera = Encoding.ASCII.GetString(b, 0, Math.Min(b.Length, 4000));
        var m = Regex.Match(cabecera, @"^\s*1\s+CHAR\s+(\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var declarada = m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
        bool utf8Valido;
        string? comoUtf8 = null;
        try { comoUtf8 = new UTF8Encoding(false, true).GetString(b); utf8Valido = true; }
        catch (DecoderFallbackException) { utf8Valido = false; }
        if (utf8Valido) return comoUtf8!;
        if (declarada == "ANSEL") return Ansel(b);
        return Windows1252(b);
    }

    private static readonly char[] Cp1252Alto =
    {
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    };

    private static string Windows1252(byte[] b)
    {
        var sb = new StringBuilder(b.Length);
        foreach (var x in b) sb.Append(x is >= 0x80 and <= 0x9F ? Cp1252Alto[x - 0x80] : (char)x);
        return sb.ToString();
    }

    /// <summary>ANSEL (el de los programas antiguos): los acentos van delante de la letra, como signos que se combinan.</summary>
    private static string Ansel(byte[] b)
    {
        var combinan = new Dictionary<byte, char>
        {
            [0xE0] = '̉', [0xE1] = '̀', [0xE2] = '́', [0xE3] = '̂', [0xE4] = '̃', [0xE5] = '̄',
            [0xE6] = '̆', [0xE7] = '̇', [0xE8] = '̈', [0xE9] = '̌', [0xEA] = '̊', [0xEB] = '͡',
            [0xEC] = '͠', [0xED] = '̕', [0xEE] = '̋', [0xEF] = '̐', [0xF0] = '̧', [0xF1] = '̨',
            [0xF2] = '̣', [0xF3] = '̤', [0xF4] = '̥', [0xF5] = '̳', [0xF6] = '̲', [0xF7] = '̦',
            [0xF8] = '̜', [0xF9] = '̮', [0xFA] = '͠', [0xFB] = '͡', [0xFE] = '̓',
        };
        var sueltos = new Dictionary<byte, char>
        {
            [0xA1] = 'Ł', [0xA2] = 'Ø', [0xA3] = 'Đ', [0xA4] = 'Þ', [0xA5] = 'Æ', [0xA6] = 'Œ', [0xA7] = 'ʹ', [0xA8] = '·',
            [0xA9] = '♭', [0xAA] = '®', [0xAB] = '±', [0xAC] = 'Ơ', [0xAD] = 'Ư', [0xAE] = 'ʼ', [0xB0] = 'ʻ', [0xB1] = 'ł',
            [0xB2] = 'ø', [0xB3] = 'đ', [0xB4] = 'þ', [0xB5] = 'æ', [0xB6] = 'œ', [0xB7] = 'ʺ', [0xB8] = 'ı', [0xB9] = '£',
            [0xBA] = 'ð', [0xBC] = 'ơ', [0xBD] = 'ư', [0xC0] = '°', [0xC1] = 'ℓ', [0xC2] = '℗', [0xC3] = '©', [0xC4] = '♯',
            [0xC5] = '¿', [0xC6] = '¡', [0xC7] = 'ß', [0xC8] = '€', [0xCF] = 'ß',
        };
        var sb = new StringBuilder(b.Length);
        var pendientes = new StringBuilder();
        foreach (var x in b)
        {
            if (combinan.TryGetValue(x, out var c)) { pendientes.Append(c); continue; }
            char ch = x < 0x80 ? (char)x : sueltos.TryGetValue(x, out var s) ? s : '�';
            sb.Append(ch);
            if (pendientes.Length > 0) { sb.Append(pendientes); pendientes.Clear(); }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
