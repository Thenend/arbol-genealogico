using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace ArbolGenealogico.Movil.Android;

[Application]
public class Aplicacion : AvaloniaAndroidApplication<App>
{
    protected Aplicacion(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }

    public override void OnCreate()
    {
        App.Plataforma = new PlataformaAndroid(this);
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithInterFont();
}
