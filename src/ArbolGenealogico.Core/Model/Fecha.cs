using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArbolGenealogico.Core.Model;

/// <summary>
/// Una fecha de la que a menudo solo se sabe el año (o el año y el mes), que puede ser aproximada («hacia 1850») y de
/// antes de Cristo. El año negativo es antes de Cristo: -44 es el 44 a. C. (no hay año 0). En el archivo es un texto:
/// «1920», «1920-03», «1920-03-05», «~1850» (aproximada) o «-0044-03-15».
/// </summary>
public readonly record struct Fecha(int Anio, int? Mes = null, int? Dia = null, bool Aproximada = false)
{
    public bool AntesDeCristo => Anio < 0;

    /// <summary>Año de 1 a 9999 (o de -9999 a -1), mes de 1 a 12 y día que exista en ese mes; día solo con mes.</summary>
    public bool EsValida =>
        Anio != 0 && Math.Abs(Anio) <= 9999
        && (Mes is null || Mes is >= 1 and <= 12)
        && (Dia is null || (Mes is { } m && Dia >= 1 && Dia <= DiasDelMes(Anio, m)));

    /// <summary>Los días de ese mes, con los años bisiestos del calendario gregoriano (también antes de Cristo).</summary>
    public static int DiasDelMes(int anio, int mes)
    {
        if (mes == 2)
        {
            int astronomico = anio < 0 ? anio + 1 : anio;          // el 1 a. C. es el año 0 de los astrónomos
            bool bisiesto = astronomico % 4 == 0 && (astronomico % 100 != 0 || astronomico % 400 == 0);
            return bisiesto ? 29 : 28;
        }
        return mes is 4 or 6 or 9 or 11 ? 30 : 31;
    }

    /// <summary>Para comparar dos fechas con lo que se sabe de las dos (el 1920 no es posterior a marzo de 1920).</summary>
    public static int Comparar(Fecha a, Fecha b)
    {
        int c = a.Anio.CompareTo(b.Anio);
        if (c != 0 || a.Mes is not { } ma || b.Mes is not { } mb) return c;
        c = ma.CompareTo(mb);
        if (c != 0 || a.Dia is not { } da || b.Dia is not { } db) return c;
        return da.CompareTo(db);
    }

    public string Guardar() =>
        (Aproximada ? "~" : "") + (Anio < 0 ? "-" : "") + Math.Abs(Anio).ToString("0000", CultureInfo.InvariantCulture)
        + (Mes is { } m ? "-" + m.ToString("00", CultureInfo.InvariantCulture) + (Dia is { } d ? "-" + d.ToString("00", CultureInfo.InvariantCulture) : "") : "");

    public override string ToString() => Guardar();

    private static readonly Regex Formato = new(@"^\s*(~)?\s*(-)?(\d{1,4})(?:-(\d{1,2})(?:-(\d{1,2}))?)?\s*$", RegexOptions.Compiled);

    /// <summary>La fecha de un texto como el que escribe <see cref="Guardar"/>, o null si no lo es o no es válida.</summary>
    public static Fecha? Leer(string? s)
    {
        if (s == null || Formato.Match(s) is not { Success: true } m) return null;
        int anio = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) * (m.Groups[2].Success ? -1 : 1);
        int? mes = m.Groups[4].Success ? int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : null;
        int? dia = m.Groups[5].Success ? int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) : null;
        var f = new Fecha(anio, mes, dia, m.Groups[1].Success);
        return f.EsValida ? f : null;
    }
}

/// <summary>Las fechas en el archivo, como texto; una que no se entienda se queda sin poner (y el árbol se abre igual).</summary>
public sealed class FechaJson : JsonConverter<Fecha?>
{
    public override bool HandleNull => true;

    public override Fecha? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? Fecha.Leer(reader.GetString()) : SaltarYNada(ref reader);

    private static Fecha? SaltarYNada(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Fecha? value, JsonSerializerOptions options)
    {
        if (value is { } f) writer.WriteStringValue(f.Guardar());
        else writer.WriteNullValue();
    }
}
