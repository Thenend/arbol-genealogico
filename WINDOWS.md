# Ramo para Windows (Microsoft Store)

La versión de Windows (`src/ArbolGenealogico.Movil.Windows`) es una ventana de escritorio hecha con el motor del móvil
(`src/ArbolGenealogico.Movil/Ordenador`): el mismo dibujo del árbol, idiomas, temas, fusión, láminas, impresión y compra,
pero con lo que se espera de un programa de ordenador, como el antiguo de WPF:

- barra de herramientas con nombre debajo de cada botón (Nuevo, Abrir, Guardar, Deshacer, Rehacer, Imprimir, Exportar,
  Fusionar, Aspecto, buscar y Ayuda); en una ventana estrecha se quedan solo los dibujos;
- los árboles son archivos `.ramo` donde se quiera, que se guardan con «Guardar» (pregunta al cerrar si hay cambios);
  los árboles enlazados se buscan junto al archivo, y «Volver» (Alt+←) regresa al anterior;
- ratón: clic elige, doble clic abre la ficha, botón derecho abre el menú de la persona, rueda para el zoom;
- teclado: los atajos de siempre (F1 los enseña): Ctrl+S/O/N/Z/Y/F/P/E/L, flechas, Intro, Supr, Insert, Inicio, Esc…;
- minimapa abajo a la derecha, ficha de la persona elegida abajo a la izquierda y panel de Aspecto;
- fotos arrastrándolas sobre una tarjeta o con Ctrl+V; exportar a PDF, PNG y GEDCOM con «Guardar como».

Para verla sin Windows: `ARBOL_PC=1` con el proyecto `ArbolGenealogico.Movil.Escritorio` (ver su `Program.cs`).

Se publica en la **Microsoft Store** porque:

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
2. **Crear una aplicación** y reservar los nombres, uno por idioma (Gestión de productos > Administrar nombres de
   aplicación), los mismos que en Google Play: «Ramo – Easy Family Tree», «Ramo – Árbol genealógico fácil»,
   «Ramo – Arbre généalogique», «Ramo – Albero genealogico», «Ramo – Einfacher Stammbaum» y «Ramo – Árvore genealógica».
   El paquete lleva cada uno en su idioma (`Paquete/Strings/<idioma>/Resources.resw`) y la tienda exige que coincidan.
3. Copiar la identidad del paquete a las variables del repositorio (arriba) y hacer el paquete.
4. **Complementos > Nuevo complemento > Duradero**, id `ramo_completo`: precio, nombre y descripción en cada idioma.
5. **Envío** de la aplicación: precio *Gratis*, mercados, clasificación por edades (cuestionario IARC), subir el
   `.msixbundle`, ficha en los seis idiomas con capturas (valen las de tablet de `tienda/promo/tablet/`, mínimo
   1366 × 768) y la dirección de la política de privacidad (la misma de Google Play).
6. Enviar a certificación (suele tardar de uno a tres días). La capacidad `runFullTrust` (todas las aplicaciones de
   escritorio la llevan) pide explicar por qué: «Es una aplicación de escritorio .NET empaquetada en MSIX».
