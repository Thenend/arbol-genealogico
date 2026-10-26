using Android.App;
using Android.Content;
using AndroidX.Core.App;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Android;

/// <summary>
/// Los avisos de cumpleaños en Android: una alarma cada día a la hora elegida (sin hora exacta, que necesitaría otro
/// permiso: Android la puede retrasar unos minutos para ahorrar batería) que mira los cumpleaños y pone una notificación
/// por cada día avisado (el mismo día y, si se quiere, unos días antes). Tocarla abre el árbol con esa persona.
/// </summary>
public static class AvisosAndroid
{
    private const string Canal = "cumpleanos";
    public const string AccionAbrir = "com.thenend.ramo.CUMPLEANOS";
    public const string ExtraRuta = "ruta", ExtraPersona = "persona";

    /// <summary>Si se abre la aplicación (o se reinicia el teléfono) pasada la hora sin haber avisado hoy, se avisa igual hasta esta hora.</summary>
    private const int HoraLimite = 21;

    private static PendingIntent Alarma(Context contexto) => PendingIntent.GetBroadcast(contexto, 0,
        new Intent(contexto, typeof(AlarmaCumpleanos)), PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

    /// <summary>Programa la próxima alarma (o la quita, sin hora).</summary>
    public static void Programar(Context contexto, int? hora)
    {
        if (contexto.GetSystemService(Context.AlarmService) is not AlarmManager am) return;
        var pi = Alarma(contexto);
        if (hora is not { } h) { am.Cancel(pi); return; }
        var ahora = DateTime.Now;
        var cuando = ahora.Date.AddHours(h);
        if (cuando <= ahora)
        {
            bool avisadoHoy = Preferencias.Actual.UltimoAviso == Hoy;
            cuando = !avisadoHoy && ahora.Hour < HoraLimite ? ahora.AddMinutes(1) : cuando.AddDays(1);
        }
        long ms = new DateTimeOffset(cuando).ToUnixTimeMilliseconds();
        // también con el teléfono en reposo
        am.SetAndAllowWhileIdle(AlarmType.RtcWakeup, ms, pi);
    }

    private static string Hoy => DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Al sonar la alarma: avisa de los cumpleaños de hoy (una vez al día) y programa la de mañana.</summary>
    public static void Avisar(Context contexto)
    {
        var pref = Preferencias.Actual;
        if (!pref.AvisosCumple) return;
        try
        {
            if (pref.UltimoAviso != Hoy)
            {
                Elegir(App.Plataforma.Idioma);
                var avisos = Movil.Avisos.DeHoy(DateTime.Today);
                pref.UltimoAviso = Hoy;
                pref.Guardar();
                foreach (var a in avisos) Mostrar(contexto, a);
            }
        }
        catch (Exception ex) { global::Android.Util.Log.Warn("ArbolGenealogico", "Avisos: " + ex.Message); }
        Programar(contexto, pref.HoraAvisos);
    }

    private static void Mostrar(Context contexto, AvisoCumple a)
    {
        var nm = NotificationManagerCompat.From(contexto);
        if (!nm.AreNotificationsEnabled()) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            nm.CreateNotificationChannel(new NotificationChannelCompat.Builder(Canal, NotificationManagerCompat.ImportanceDefault)
                .SetName(T("Cumpleaños"))!.Build());
        var abrir = new Intent(contexto, typeof(MainActivity));
        abrir.SetAction(AccionAbrir);
        abrir.PutExtra(ExtraRuta, a.Primera.Ruta);
        abrir.PutExtra(ExtraPersona, a.Primera.Id);
        abrir.AddFlags(ActivityFlags.NewTask);
        var pi = PendingIntent.GetActivity(contexto, 100 + a.Dias, abrir, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var n = new NotificationCompat.Builder(contexto, Canal)
            .SetSmallIcon(Resource.Drawable.aviso)!
            .SetContentTitle(a.Titulo)!
            .SetContentText(a.Texto)!
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(a.Texto))!
            .SetContentIntent(pi)!
            .SetAutoCancel(true)!
            .SetCategory(NotificationCompat.CategoryReminder)!
            .Build()!;
        try { nm.Notify(100 + a.Dias, n); }
        catch (Java.Lang.SecurityException) { }
    }
}

/// <summary>La alarma de cada día.</summary>
[BroadcastReceiver(Exported = false)]
public class AlarmaCumpleanos : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        // leer los árboles puede tardar: fuera del hilo principal, con Android esperando
        var pendiente = GoAsync();
        Task.Run(() =>
        {
            try { AvisosAndroid.Avisar(context); }
            finally { pendiente?.Finish(); }
        });
    }
}

/// <summary>Al reiniciar el teléfono, al actualizar la aplicación o al cambiar la hora, las alarmas se pierden: se vuelven a programar.</summary>
[BroadcastReceiver(Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged })]
public class ArranqueCumpleanos : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        var pref = Preferencias.Actual;
        AvisosAndroid.Programar(context, pref.AvisosCumple ? pref.HoraAvisos : null);
    }
}
