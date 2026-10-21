# Ramo para Android

La aplicación del móvil funciona **sin servidor y sin conexión**. Los árboles se guardan en el teléfono, se mandan como
archivos `.ramo` por WhatsApp, Telegram, correo, Bluetooth o Quick Share, y se abren tocándolos. El mismo archivo se
abre en la aplicación de escritorio y en la web. Lo único que usa internet es Google Play, para comprar «Ramo completo»
(ver [Versión gratuita y Ramo completo](#versión-gratuita-y-ramo-completo)).

## Cómo funciona compartir sin servidor
- **Mandar**: en «Mis árboles» (⋮ de cada árbol) o en el menú del árbol, **Compartir el árbol** abre el menú «Compartir»
  de Android con el archivo `.ramo`. Si alguna tarjeta enlaza con otro árbol que está en el teléfono, ofrece mandar
  también los enlazados. **Compartir como PDF** manda todo el árbol en un PDF vectorial de una página: se amplía sin
  perder nitidez (también los árboles muy grandes) y se pueden buscar nombres en él. WhatsApp y el correo lo mandan
  como documento, sin reducirlo. **Compartir como imagen** manda un PNG, que solo sirve para árboles pequeños: WhatsApp
  reduce las fotos a unos 2000 px y en un árbol grande los nombres dejan de leerse.
- **Imprimir** (menú del árbol): como el modo de impresión del escritorio. Se elige el papel (A4, A3, A2, A1 o los de
  Estados Unidos: Carta, Legal y Tabloide; la primera vez, el habitual en el país del teléfono), en cuántas hojas (1 a 10)
  y los colores (claro, oscuro o blanco y negro, con las fotos en grises). Arriba se ven las hojas ya pegadas con el árbol
  como saldrá y las líneas de corte (se amplía con dos dedos o con dos toques y se mueve con un dedo, para ver de cerca
  cómo saldrán las tarjetas; el botón de la esquina vuelve a enseñar todas las hojas); debajo, cómo se reparten, de cuántos puntos sale la letra de los nombres (en verde si
  se lee bien, en dorado si es pequeña y en rojo si no se leerá) y si algún corte parte tarjetas. **Imprimir** abre la
  impresión de Android (impresoras de la red o «Guardar como PDF») con el papel y la orientación ya puestos; **Compartir
  PDF** manda las hojas para imprimirlas en otro sitio. Cada hoja lleva sus márgenes con la línea de corte y la zona gris
  donde se pega la siguiente. El cálculo de las hojas es el mismo del escritorio (`PlanImpresion`, en el núcleo).
- **Recibir**: al tocar un `.ramo` en WhatsApp, el correo o el gestor de archivos, Android ofrece «Ramo».
  También se puede **compartir** el archivo con la aplicación o usar **Abrir archivo** en «Mis árboles».
  La aplicación pregunta qué hacer:
  - **Abrirlo**: se guarda como un árbol más. Si ya estaba exactamente igual, se abre el que había.
  - **Fusionarlo con «…»**: se proponen los árboles del teléfono que comparten personas con el recibido. Se añaden las
    personas y uniones que falten y se completan los datos vacíos (fotos, historias…). Si un dato está en los dos y es
    distinto, se elige cuál se queda. La fusión se puede deshacer.
  - **Fusionarlo con otro árbol…**: para árboles empezados por separado. La aplicación propone quién parece la misma
    persona (mismo nombre y familia parecida), y también se puede indicar a mano.
- Las personas se reconocen entre árboles porque cada una tiene un **id único** (ver «Formato del archivo» en el
  [README](README.md)). Por eso dos copias del mismo árbol se fusionan solas, aunque cada familiar haya añadido cosas.

## Uso
| Acción | Cómo |
|---|---|
| Mover / zoom | Un dedo mueve (con inercia), dos dedos hacen zoom. Botón de abajo a la derecha: ver todo. El árbol no se puede perder de vista |
| Acercar / alejar de golpe | Doble toque en el fondo: alterna entre las tarjetas grandes (para leerlas) y mucho árbol a la vista (para moverse), centrando el punto tocado |
| Ver el linaje de alguien | Tocar su tarjeta. Abajo aparece su ficha con los botones |
| Saber quién tiene historia | Las personas con algo escrito en «Su historia» llevan un circulito con renglones (en la foto o en la esquina). Se quita en el menú ⋮ → Historia → Sin |
| Fechas y lugar de nacimiento, defunción | En la ficha: el lugar de nacimiento (texto libre) y, para las fechas, día, mes y año, pero basta con el año (el mes y el día, si se saben); se pueden marcar como aproximadas («hacia 1850»), y las de antes de Cristo se escriben con el año en negativo (`-44`; debajo del año se ve cómo se lee: «44 a. C.»). «Ha fallecido» se marca aunque no se sepa cuándo (la fecha de defunción solo se pide entonces). Las tarjetas muestran los años («1920 – 1998», «* 1950», «† 1890», «1850 – ?» si falleció sin saberse cuándo; se quitan en el menú ⋮ → Fechas → Sin) y la parte de abajo, las fechas enteras y el lugar («* 1955 en Ávila»). Al fusionar, la fecha que falta se completa y, si las dos dicen lo mismo, se queda la que sabe más |
| Ver la foto en grande | Tocar la foto en la parte de abajo (con la tarjeta seleccionada) o en la ficha |
| Ficha (editar) | Tocar otra vez la tarjeta seleccionada (o doble toque), o tocar su nombre en la parte de abajo (o su foto, si no tiene). Foto desde la galería o la cámara, encuadrada en el círculo con los dedos («Encuadrar» la recoloca después) |
| Añadir familiares | Mantener pulsada la tarjeta, el «+» de la tarjeta seleccionada o «Familiar» |
| Árbol enlazado | Botón dorado de la tarjeta o «Su árbol»; «Atrás» vuelve al anterior |
| Buscar | Lupa de arriba; las flechas pasan de una coincidencia a otra |
| Deshacer, fusionar, nombre del árbol, guardar una copia | Menú ⋮ |
| GEDCOM | Exportar: menú ⋮ del árbol o ⋮ de «Mis árboles» → «Exportar a GEDCOM». Importar: «Abrir archivo», o tocar un `.ged` en otra aplicación; como cualquier árbol recibido, se puede abrir o fusionar con uno tuyo. GEDCOM (`.ged`), el formato de los demás programas de genealogía (Ancestry, MyHeritage, FamilySearch, Geneanet…). **Importar**: se abre como cualquier árbol; las fechas de nacimiento y defunción y el lugar de nacimiento van a la ficha como dato (también las aproximadas y las de antes de Cristo; las de «antes de», «después de» o «entre» se quedan en la historia), y lo que Ramo no guarda como dato (lugares, bautizo, boda, entierro, oficios, residencias…) se escribe al principio de la historia de cada persona en frases («Fue bautizado en Ávila. Oficio: Agricultor…») y detrás van sus notas. Lee UTF-8, UTF-16, ANSEL y Windows-1252; las fotos de GEDCOM no se importan. **Exportar**: GEDCOM 5.5.1 en UTF-8 con nombres, sexo, fechas de nacimiento y defunción, lugar de nacimiento, parejas, hijos y la historia como nota, sin fotos; cada persona lleva su id (`_UID`), así que un árbol que sale de Ramo y vuelve se reconoce y se puede fusionar |
| Colocación del árbol y forma de las tarjetas | Menú ⋮: botones con dibujo, la elegida en dorado, cada cosa en su grupo (colocación: Compacto, Lateral, Balanceado, Escalonado; con o sin foto; horizontal o vertical; con o sin tarjeta; con o sin apellidos; con o sin los años; con o sin la marca de la historia). Al tocar uno el árbol cambia detrás y el menú sigue abierto, para cambiar varios; se cierra con «Atrás» o tocando fuera |

**Tamaño de letra.** Los textos de la aplicación siguen el tamaño de fuente de Android (Ajustes → Pantalla → Tamaño de
fuente), hasta el doble. Con la letra muy grande (más de 1,25), lo que va en fila se pone en columna y los textos largos
pasan a varias líneas; los iconos crecen algo menos que la letra. Las tarjetas del árbol no cambian (el árbol se amplía con
los dedos), pero se abre más ampliado.

Todo se guarda al momento. Los datos están en la carpeta privada de la aplicación, que entra en la copia de seguridad
automática de Android (en la cuenta de Google del usuario).

**Copia en Documentos.** En Android 10 y posteriores, cada vez que se guarda un árbol se escribe también una copia en
`Documentos/Ramo` (hasta el 18-10-2026, `Documentos/Árbol Genealógico`; esa carpeta ya no se usa) (con MediaStore, sin pedir permisos), y al abrir la aplicación se copian todos. Esa carpeta
no se borra al desinstalar la aplicación ni al borrar sus datos. Si «Mis árboles» está vacío, **Recuperar mis árboles**
abre el selector de carpetas de Android ya en esa carpeta: con «Usar esta carpeta» y «Permitir» se recuperan todos. El
permiso sobre la carpeta se conserva, y desde entonces las copias se reescriben a través de él. Sin ese permiso, tras
reinstalar, Android no deja ver las copias antiguas y se crearían «nombre (1).ramo». Borrar un árbol en la aplicación
deja su copia. La copia se activa y desactiva en el botón ⓘ de «Mis árboles».

## Versión gratuita y Ramo completo
La aplicación es gratuita y tiene una sola compra dentro, **«Ramo completo»**: un pago único (producto de compra única,
no una suscripción), sin servidor propio. Lo que incluye cada versión está en `Licencia.cs`, con los límites como
constantes (`ArbolesGratis`, `PersonasGratis`):

| | Gratis | Ramo completo |
|---|---|---|
| Ver y editar | Todos los árboles, también los recibidos, sin límite | Igual |
| Árboles propios (creados con «Nuevo árbol» o importados de GEDCOM) | 1 (los recibidos, el de ejemplo y los recuperados de la copia no cuentan) | Sin límite |
| Personas | Se añaden mientras el árbol tenga menos de 50 (uno recibido con más se edita, pero no crece) | Sin límite |
| Fusionar | Si el árbol queda con 50 o menos (o si no añade a nadie) | Sin límite |
| Enlazar el árbol de una persona | No (abrir los enlaces que ya traen los árboles, sí; quitar un enlace, también) | Sí |
| PDF, imagen y hojas para imprimir | Con «Hecho con Ramo» (abajo a la derecha; en las hojas, en el margen de arriba de la hoja de arriba a la derecha, que no se recorta) | Sin marca |

Al llegar a un límite sale la hoja «Ramo completo» (`PantallaCompleta.cs`): por qué sale, lo que incluye, el precio que
da Google Play, **Comprar**, **Restaurar compra** y **Ahora no**. También se abre con la estrella de «Mis árboles», que
solo se ve en la versión gratuita. Qué árboles son propios lo apunta la biblioteca en `propios.json`.

La compra (`TiendaGooglePlay.cs`, Google Play Billing 9): se consulta el producto, se abre la ventana de pago de Google
Play y cada compra pagada se **confirma** («acknowledge») enseguida (si no, Google la devuelve sola a los tres días). Los
pagos pendientes (efectivo en una tienda…) se activan solos cuando se completan. Que se ha comprado se recuerda en las
preferencias y se vuelve a preguntar a Google Play cada vez que se abre la aplicación: si no hay conexión, se queda como
estaba; si Google Play dice que no hay compra (p. ej. se devolvió), se quita.

**En Play Console**: Monetizar → Productos → **Productos únicos** → Crear producto, con el id **`ramo_completo`** (el de
`Licencia.Producto`), el nombre «Ramo completo» (y sus traducciones), una descripción y el precio; después, activarlo.
Hasta que la aplicación no esté subida a alguna pista (la de pruebas internas basta), Play Console no deja crear
productos, y hasta que el producto esté activo la aplicación dice que «Ramo completo no está a la venta ahora mismo».

**Para probar la compra sin pagar**: Configuración → **Pruebas de licencias**, añadir las cuentas de Google de quienes
prueban; instalar la aplicación desde el enlace de la pista de pruebas internas (no con adb) con una de esas cuentas. En
la ventana de pago sale «Tarjeta de prueba: siempre se aprueba» (y otras para probar el rechazo o el pago pendiente).
Las compras de prueba se reembolsan desde Pedidos.

**Sin tienda**: compilando con `-p:SinTienda=true` no hay Google Play Billing y todo está incluido (para instalarla uno
mismo con adb, fuera de Google Play). En el simulador hay una tienda de prueba: `ARBOL_COMPLETA=1` empieza con la compra
hecha y `ARBOL_COMPRA=pendiente|cancelada|fallida` dice cómo acaba la compra.

## Proyectos
- `src/ArbolGenealogico.Movil`: la aplicación (Avalonia 12), igual en cualquier sistema. Usa `ArbolGenealogico.Core`
  (modelo, colocación, `.ramo` y fusión).
- `src/ArbolGenealogico.Movil.Android`: lo propio de Android (abrir archivos de otras aplicaciones, «Compartir»,
  cámara) y la compra de «Ramo completo» con Google Play Billing (`TiendaGooglePlay.cs`). La biblioteca de Billing añade
  el permiso de internet, que solo usa para hablar con Google Play.
- `src/ArbolGenealogico.Movil.Escritorio`: la misma aplicación en una ventana de Windows, para probarla sin teléfono.
  Con `--captura salida.png [ancho alto] [pasos…]` guarda una captura (ver `Program.cs`); `ARBOL_DATOS=carpeta` usa otra
  carpeta de datos, `ARBOL_ESCALA=2.5` da capturas de 1080 × 2160, `ARBOL_LETRA=2` simula el tamaño de fuente de Android
  y `ARBOL_IDIOMA=de` el idioma del teléfono.

## Idiomas
La aplicación del móvil está en español, inglés, francés, italiano, alemán y portugués de Brasil, y sale en el idioma
del teléfono (si es otro, en inglés). Desde Android 13 se puede elegir uno solo para Ramo en Ajustes → Aplicaciones →
Ramo → Idioma (los idiomas que ofrece están en `Resources/xml/idiomas.xml`). El escritorio y la web siguen en español.
- En el código los textos se escriben en español dentro de `T("…")` (o `N(n, "1 persona", "{0} personas")` si llevan
  número), y ese texto es la clave de su traducción en `src/ArbolGenealogico.Core/Idiomas` (`Ingles.cs`, `Frances.cs`…).
  Una clave terminada en `|F` es la forma de mujer de un texto que cambia con el sexo («Né» / «Née»).
- Al cambiar o añadir un texto hay que ponerlo en las cinco tablas: `IdiomaTests` falla si falta alguno, si sobra o si
  los huecos `{0}` no coinciden.
- Las frases que escribe el GEDCOM al importar (fechas, «Nació el 3 de marzo de 1920 en…») tienen su gramática de
  cada idioma en `Gedcom.cs`, y el árbol de ejemplo se traduce al abrirlo («Tía Rosa», sus historias).

Hace falta el SDK de .NET 10 con la carga de trabajo `android` (la instala Visual Studio) y el SDK de Android con un JDK
(los instala también Visual Studio).

```
dotnet run --project src/ArbolGenealogico.Movil.Escritorio                 # probar en el ordenador
dotnet build src/ArbolGenealogico.Movil.Android -c Release                 # compilar el .apk
adb install -r src/ArbolGenealogico.Movil.Android/bin/Release/net10.0-android/com.thenend.arbolgenealogico-Signed.apk   # instalarlo en el teléfono conectado (depuración USB)
```

**No usar `dotnet build -t:Install` en un teléfono con árboles de verdad.** Para reinstalar una versión Release, esa orden
desinstala antes la aplicación (`pm uninstall -k`, dos veces a la vez) y luego la instala. Se supone que conserva los datos,
pero el 17-10-2026, en un Samsung con Android 13, una de esas reinstalaciones dejó vacía la carpeta de la aplicación y se
perdieron los árboles. `adb install -r` reemplaza la aplicación sin desinstalarla y conserva los datos.

## Publicar en Google Play

### 1. Una vez: la clave de firma
Google Play firma la aplicación con su propia clave («Firma de aplicaciones de Play»). Tú solo necesitas una **clave de
subida**. Créala tú y guarda el archivo y la contraseña en un lugar seguro, fuera del repositorio. Si se pierde, se puede
pedir a Google que la cambie, pero cuesta días.

```
"C:\Program Files\Android\openjdk\jdk-21.0.8\bin\keytool.exe" -genkeypair -v -keystore %USERPROFILE%\arbol-subida.keystore -alias arbol -keyalg RSA -keysize 2048 -validity 10000
```

### 2. Cada versión: el paquete .aab
Sube `ApplicationVersion` (un número entero, +1 cada vez) y `ApplicationDisplayVersion` en
`src/ArbolGenealogico.Movil.Android/ArbolGenealogico.Movil.Android.csproj`. Después:

```
set ARBOL_CLAVE=la-contraseña
dotnet publish src/ArbolGenealogico.Movil.Android -c Release -p:AndroidPackageFormat=aab -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=%USERPROFILE%\arbol-subida.keystore -p:AndroidSigningKeyAlias=arbol -p:AndroidSigningKeyPass=env:ARBOL_CLAVE -p:AndroidSigningStorePass=env:ARBOL_CLAVE -o dist/android
```

El archivo que se sube es `dist/android/com.thenend.arbolgenealogico-Signed.aab`.

### 3. Play Console
1. Cuenta de desarrollador en <https://play.google.com/console> (pago único de 25 USD y verificación de identidad).
   Para cobrar la aplicación hace falta además el **perfil de pagos** (Configuración → Perfil de pagos).
2. **Crear aplicación**: nombre «Ramo – Easy Family Tree», idioma predeterminado **inglés (en-US)** (el que ve quien
   busca en un idioma sin ficha propia, como la aplicación, que en esos idiomas sale en inglés), aplicación (no juego),
   **gratuita**: lo que
   se paga es la compra dentro de la aplicación «Ramo completo» (ver
   [Versión gratuita y Ramo completo](#versión-gratuita-y-ramo-completo)).
3. **Contenido de la aplicación**:
   - Política de privacidad: `https://thenend.github.io/ramo/privacidad-android.html`. Este repositorio es privado, así
     que la web pública de Ramo (la portada y la política, en seis idiomas) está en `tienda/web` y se publica en el
     repositorio público [Thenend/ramo](https://github.com/Thenend/ramo) con GitHub Pages: al cambiarla, se copia allí
     lo de `tienda/web` y se sube. La web de la portada (`https://thenend.github.io/ramo/`) vale también como sitio web
     de la ficha.
   - Seguridad de los datos: **no recoge datos** y **no comparte datos** (todo se queda en el teléfono, también la copia
     en Documentos; lo que el usuario manda con «Compartir» lo manda él con otra aplicación). La compra la hace Google
     Play con la cuenta del usuario: la aplicación no envía nada a ningún servidor.
   - Anuncios: no. Acceso a la aplicación: no hace falta cuenta. Público objetivo: mayores de 18 (o 13+).
   - Clasificación de contenido: cuestionario, todo «no».
4. **Ficha de Play Store** en seis idiomas (textos en `tienda/ficha.md`), icono `tienda/icono-play-512.png`, imagen
   destacada `tienda/destacada/<idioma>.png` y capturas `tienda/capturas/<idioma>/*.png` (1080 × 2160). El icono (el ramo) se genera con
   `dotnet run tienda/ramo.cs -- todo …` (Android, Play, escritorio y web; ver el comentario del principio) y la imagen
   destacada con `node tienda/destacada-fuente/generar.js` (Playwright y Chromium; dibuja `destacada.html` en cada
   idioma, con la captura `1-arbol.png` de ese idioma en el móvil, así que se rehace después de las capturas). Las capturas, con la aplicación de
   escritorio, `ARBOL_ESCALA=2.5`, `ARBOL_IDIOMA` y los pasos `abrir:Ejemplos/familia-garcia.ramo sel:<Padre>`,
   `ejemplo inicio`, `abrir:Ejemplos/familia-perez.ramo recibir:<árbol>`, `abrir:… fusionar:<árbol>|Ana` y
   `abrir:… ficha:<Abuelo paterno>` (con los nombres del ejemplo en ese idioma).
   Las que se suben a Play Console son las de `tienda/promo/telefono/<idioma>/` (1080 × 1920, 9:16) y
   `tienda/promo/tablet/<idioma>/` (2560 × 1440, 16:9, para tabletas de 7 y de 10 pulgadas), ocho por idioma
   numeradas en el orden en que se suben: el logo de Ramo, un titular y la pantalla dentro del aparato, hechas con
   `python tienda/promo/enmarcar.py` (Pillow) a partir de las pantallas en bruto de `tienda/promo/crudas` (se sacan
   con `python tienda/promo/crudas/capturar.py`, ver su LEEME.md). Las de
   `tienda/capturas` (1080 × 2160) no valen tal cual: Play pide 16:9 o 9:16.
5. **Prueba cerrada**: las cuentas personales creadas después de noviembre de 2023 tienen que hacer una prueba cerrada
   con **al menos 12 personas durante 14 días seguidos** antes de poder publicar. Lo natural es la propia familia: se
   añaden sus correos de Google como testers, aceptan la invitación desde el enlace e instalan la aplicación.
6. Pasados los 14 días, **solicitar acceso a producción** y publicar.

### Textos de la ficha
Están en [tienda/ficha.md](tienda/ficha.md), en los seis idiomas: nombre («Ramo – Easy Family Tree», «Ramo – Árbol
genealógico fácil»…), descripción breve y completa.

**Categoría**: Estilo de vida (o Educación). **Etiquetas**: genealogía, árbol familiar, familia.
