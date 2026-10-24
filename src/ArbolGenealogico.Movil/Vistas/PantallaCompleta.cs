using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static ArbolGenealogico.Core.Idiomas.Idioma;

namespace ArbolGenealogico.Movil.Vistas;

/// <summary>
/// «Ramo completo»: lo que incluye, el precio y el botón para comprarlo (y «Restaurar compra»). Sale al llegar a un límite
/// de la versión gratuita, contando por qué, o desde la estrella de «Mis árboles»; nunca de golpe ni más de una vez.
/// </summary>
public static class PantallaCompleta
{
    /// <summary>Lo ofrece. Devuelve true si al cerrarse ya es la versión completa (y lo que se quería hacer puede seguir).</summary>
    /// <param name="motivo">Por qué sale (el límite al que se ha llegado), o null si se ha abierto a propósito.</param>
    public static async Task<bool> Ofrecer(string? motivo = null)
    {
        if (Licencia.Completa && motivo != null) return true;
        var p = Principal.Actual;
        string? aviso = null;
        bool ok = await p.HojaAbajo<bool>(cerrar =>
        {
            // lo que cuenta se desplaza si no cabe (con la letra muy grande); los botones quedan siempre a la vista, abajo
            var pila = new StackPanel { Spacing = 12, Margin = new Thickness(22, 8, 22, 4) };
            var botones = new StackPanel { Spacing = 10, Margin = new Thickness(22, 10, 22, 0) };
            var raiz = new DockPanel();
            DockPanel.SetDock(botones, Dock.Bottom);
            raiz.Children.Add(botones);
            raiz.Children.Add(new ScrollViewer { Content = pila });

            var cabecera = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var estrella = Ui.Icono(Ui.Iconos.Estrella, 28, Ui.Oro);
            estrella.Margin = new Thickness(0, 0, 12, 0); estrella.VerticalAlignment = VerticalAlignment.Center;
            cabecera.Children.Add(estrella);
            var titulo = Ui.Titulo(T("Ramo completo"), 22); titulo.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(titulo, 1); cabecera.Children.Add(titulo);
            pila.Children.Add(cabecera);

            if (Licencia.Completa)
            {
                pila.Children.Add(Ui.Parrafo(T("Ya tienes Ramo completo: árboles y personas sin límite, árboles enlazados y todo lo que exportas sin marca. ¡Gracias!"), 15, Ui.Texto));
                var aceptar = Ui.Boton(T("Aceptar"), () => cerrar(true), principal: true);
                aceptar.HorizontalAlignment = HorizontalAlignment.Stretch;
                botones.Children.Add(aceptar);
                return raiz;
            }

            if (motivo != null) pila.Children.Add(Ui.Parrafo(motivo, 15, Ui.Texto));

            // lo que incluye
            var lista = new StackPanel { Spacing = 8 };
            foreach (var s in new[]
            {
                T("Árboles sin límite"),
                T("Sin límite de personas en cada árbol"),
                T("Enlazar los árboles de varias familias"),
                T("PDF, imágenes e impresiones sin la marca «Hecho con Ramo»"),
                T("Láminas para regalar, con título, dedicatoria y marco"),
            })
            {
                var fila = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                var ic = Ui.Icono(Ui.Iconos.Hecho, 20, Ui.Verde, 2.4);
                ic.Margin = new Thickness(0, 0, 12, 0); ic.VerticalAlignment = VerticalAlignment.Top;
                fila.Children.Add(ic);
                var t = new TextBlock { Text = s, FontSize = Ui.L(15), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(t, 1); fila.Children.Add(t);
                lista.Children.Add(fila);
            }
            pila.Children.Add(new Border
            {
                Child = lista, Background = Ui.Fondo, CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 12),
                BorderBrush = Ui.Linea, BorderThickness = new Thickness(1),
            });
            pila.Children.Add(Ui.Parrafo(T("Un solo pago, para siempre: sin suscripción. Lo que ya tienes sigue igual: tus árboles y los que te manden se ven y se editan sin pagar."), 14));

            var textoComprar = new TextBlock
            {
                Text = T("Comprar"), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                Foreground = Ui.BotonTexto,
            };
            var comprar = new Button { Content = textoComprar, Classes = { "principal" }, HorizontalAlignment = HorizontalAlignment.Stretch };
            var restaurar = Ui.Boton(T("Restaurar compra"), () => { });
            var ahoraNo = Ui.Boton(T("Ahora no"), () => cerrar(false));
            botones.Children.Add(comprar);
            var abajo = Ui.LetraGrande
                ? new Grid { RowDefinitions = new RowDefinitions("Auto,8,Auto") }
                : new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*") };
            restaurar.HorizontalAlignment = HorizontalAlignment.Stretch;
            ahoraNo.HorizontalAlignment = HorizontalAlignment.Stretch;
            abajo.Children.Add(restaurar);
            if (Ui.LetraGrande) Grid.SetRow(ahoraNo, 2); else Grid.SetColumn(ahoraNo, 2);
            abajo.Children.Add(ahoraNo);
            botones.Children.Add(abajo);

            // el precio lo da la tienda (con la moneda del país); hasta entonces, solo «Comprar»
            _ = PonerPrecio();
            async Task PonerPrecio()
            {
                try
                {
                    if (await App.Plataforma.Tienda!.Precio() is { } precio) textoComprar.Text = T("Comprar por {0}", precio);
                }
                catch { }
            }

            void Ocupado(bool si) { comprar.IsEnabled = !si; restaurar.IsEnabled = !si; }

            comprar.Click += async (_, _) =>
            {
                Ocupado(true);
                try
                {
                    var (r, porque) = await App.Plataforma.Tienda!.Comprar();
                    switch (r)
                    {
                        case ResultadoCompra.Hecha or ResultadoCompra.YaComprada:
                            aviso = r == ResultadoCompra.Hecha ? T("¡Gracias! Ya tienes Ramo completo") : T("Ya tenías Ramo completo con esta cuenta: activado");
                            cerrar(true);
                            return;
                        case ResultadoCompra.Pendiente:
                            await p.Avisar(T("Pago pendiente"), T("Cuando se complete el pago, Ramo completo se activará solo."));
                            cerrar(false);
                            return;
                        case ResultadoCompra.Fallida:
                            await p.Avisar(T("No se pudo completar la compra"),
                                porque ?? T("Comprueba la conexión a internet y que {0} funcione con tu cuenta.", App.Plataforma.Tienda?.Nombre));
                            break;
                    }
                }
                catch (Exception ex) { await p.Avisar(T("No se pudo completar la compra"), ex.Message); }
                Ocupado(false);
            };

            restaurar.Click += async (_, _) =>
            {
                Ocupado(true);
                var r = await Licencia.Restaurar();
                if (r == true)
                {
                    aviso = T("Compra restaurada: ya tienes Ramo completo");
                    cerrar(true);
                    return;
                }
                if (r == false)
                    await p.Avisar(T("No hay ninguna compra"), T("Esta cuenta de {0} no tiene Ramo completo. Si lo compraste con otra, ábrela en {0} y vuelve a probar.", App.Plataforma.Tienda?.Nombre));
                else
                    await p.Avisar(T("No se pudo comprobar"), T("Comprueba la conexión a internet y vuelve a probar."));
                Ocupado(false);
            };
            return raiz;
        }, false);
        if (aviso != null) p.Aviso(aviso, 3.5);
        return ok && Licencia.Completa;
    }

    // Por qué sale, en cada límite.

    public static Task<bool> PorArboles() => Ofrecer(N(Licencia.ArbolesGratis,
        "Con la versión gratuita puedes tener 1 árbol propio. Los que te mandan tus familiares no cuentan: esos puedes abrirlos todos.",
        "Con la versión gratuita puedes tener {0} árboles propios. Los que te mandan tus familiares no cuentan: esos puedes abrirlos todos."));

    public static Task<bool> PorPersonas(int tiene) => Ofrecer(T(
        "Con la versión gratuita, cada árbol puede tener hasta {0} personas, y este ya tiene {1}. Puedes seguir viéndolo y cambiando lo que quieras de las que ya están.",
        Licencia.PersonasGratis, tiene));

    public static Task<bool> PorFusion(int quedarian) => Ofrecer(T(
        "Con la versión gratuita, cada árbol puede tener hasta {0} personas, y al fusionar quedarían {1}.",
        Licencia.PersonasGratis, quedarian));

    public static Task<bool> PorEnlace() => Ofrecer(T(
        "Enlazar el árbol de una persona (por ejemplo, el de la familia de tu pareja) para pasar de uno a otro es parte de Ramo completo."));
}
