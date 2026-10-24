# Ramo para Windows (Microsoft Store)

La versión de Windows es la misma aplicación del móvil (`src/ArbolGenealogico.Movil.Windows`) en una ventana con forma de
teléfono. Se publica en la **Microsoft Store** porque:

- registrarse como desarrollador individual es gratis;
- la tienda firma el paquete, así que no sale el aviso de SmartScreen («Windows protegió su PC»), que asusta a quien no
  está acostumbrado a instalar programas;
- se instala con un botón, se actualiza sola y cobra «Ramo completo» con la cuenta Microsoft de Windows.

## Ramo completo
Es un **complemento duradero** de la tienda con el id de producto `ramo_completo` (el mismo que en Google Play). La
aplicación lo busca por ese id (`TiendaMicrosoft.cs`), pregunta a la tienda si la cuenta lo tiene al abrirse y en
«Restaurar compra», y abre la ventana de pago de Windows al comprar. Sin servidor propio.

Fuera de la tienda (el `.zip` suelto, sin paquete) no hay tienda a la que preguntar y todo está incluido.

## Hacer el paquete
Lo hace GitHub: pestaña **Actions > Windows > Run workflow**, con la versión (cuatro números: `1.0.1.0`). Al acabar,
en esa ejecución, en «Artifacts», está `Ramo-<versión>.msixbundle` (x64 y ARM juntos). En un Windows con el SDK de
Windows también se puede hacer a mano con `src/ArbolGenealogico.Movil.Windows/Paquete/empaquetar.ps1`.

Antes, una sola vez, hay que copiar la identidad del paquete de Partner Center (Ramo > Gestión de productos >
Identidad del producto) a las variables del repositorio (Settings > Secrets and variables > Actions > Variables):

| Variable | En Partner Center |
|---|---|
| `MSSTORE_NOMBRE` | Package/Identity/Name |
| `MSSTORE_EDITOR` | Package/Identity/Publisher (`CN=…`) |
| `MSSTORE_EDITOR_VISIBLE` | Package/Properties/PublisherDisplayName |

Cada envío a la tienda necesita una versión mayor que la anterior.

## Publicar (Partner Center)
1. Cuenta de desarrollador individual en https://storedeveloper.microsoft.com (gratis, con verificación de identidad).
2. **Crear una aplicación** y reservar el nombre «Ramo».
3. Copiar la identidad del paquete a las variables del repositorio (arriba) y hacer el paquete.
4. **Complementos > Nuevo complemento > Duradero**, id `ramo_completo`: precio, nombre y descripción en cada idioma.
5. **Envío** de la aplicación: precio *Gratis*, mercados, clasificación por edades (cuestionario IARC), subir el
   `.msixbundle`, ficha en los seis idiomas con capturas (valen las de tablet de `tienda/promo/tablet/`, mínimo
   1366 × 768) y la dirección de la política de privacidad (la misma de Google Play).
6. Enviar a certificación (suele tardar de uno a tres días). La capacidad `runFullTrust` (todas las aplicaciones de
   escritorio la llevan) pide explicar por qué: «Es una aplicación de escritorio .NET empaquetada en MSIX».
