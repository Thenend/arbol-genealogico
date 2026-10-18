# Ramo para Android

La aplicación del móvil funciona **sin servidor y sin internet**. Los árboles se guardan en el teléfono, se mandan como
archivos `.ramo` por WhatsApp, Telegram, correo, Bluetooth o Quick Share, y se abren tocándolos. El mismo archivo se
abre en la aplicación de escritorio y en la web.

## Cómo funciona compartir sin servidor
- **Mandar**: en «Mis árboles» (⋮ de cada árbol) o en el menú del árbol, **Compartir el árbol** abre el menú «Compartir»
  de Android con el archivo `.ramo`. Si alguna tarjeta enlaza con otro árbol que está en el teléfono, ofrece mandar
  también los enlazados. **Compartir como PDF** manda todo el árbol en un PDF vectorial de una página: se amplía sin
  perder nitidez (también los árboles muy grandes) y se pueden buscar nombres en él. WhatsApp y el correo lo mandan
  como documento, sin reducirlo. **Compartir como imagen** manda un PNG, que solo sirve para árboles pequeños: WhatsApp
  reduce las fotos a unos 2000 px y en un árbol grande los nombres dejan de leerse.
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
| Fechas de nacimiento y defunción | En la ficha: día, mes y año, pero basta con el año (el mes y el día, si se saben); se pueden marcar como aproximadas («hacia 1850») y de antes de Cristo. Las tarjetas muestran los años («1920 – 1998», «* 1950», «† 1890»; se quitan en el menú ⋮ → Fechas → Sin) y la parte de abajo, las fechas enteras. Al fusionar, la fecha que falta se completa y, si las dos dicen lo mismo, se queda la que sabe más |
| Ver la foto en grande | Tocar la foto en la parte de abajo (con la tarjeta seleccionada) o en la ficha |
| Ficha (editar) | Tocar otra vez la tarjeta seleccionada (o doble toque), o tocar su nombre en la parte de abajo (o su foto, si no tiene). Foto desde la galería o la cámara, encuadrada en el círculo con los dedos («Encuadrar» la recoloca después) |
| Añadir familiares | Mantener pulsada la tarjeta, el «+» de la tarjeta seleccionada o «Familiar» |
| Árbol enlazado | Botón dorado de la tarjeta o «Su árbol»; «Atrás» vuelve al anterior |
| Buscar | Lupa de arriba; las flechas pasan de una coincidencia a otra |
| Deshacer, fusionar, nombre del árbol, guardar una copia | Menú ⋮ |
| GEDCOM | Exportar: menú ⋮ del árbol o ⋮ de «Mis árboles» → «Exportar a GEDCOM». Importar: «Abrir archivo», o tocar un `.ged` en otra aplicación; como cualquier árbol recibido, se puede abrir o fusionar con uno tuyo. GEDCOM (`.ged`), el formato de los demás programas de genealogía (Ancestry, MyHeritage, FamilySearch, Geneanet…). **Importar**: se abre como cualquier árbol; las fechas de nacimiento y defunción van a la ficha como dato (también las aproximadas y las de antes de Cristo; las de «antes de», «después de» o «entre» se quedan en la historia), y lo que Ramo no guarda como dato (lugares, bautizo, boda, entierro, oficios, residencias…) se escribe al principio de la historia de cada persona en frases («Nació en Ávila. Oficio: Agricultor…») y detrás van sus notas. Lee UTF-8, UTF-16, ANSEL y Windows-1252; las fotos de GEDCOM no se importan. **Exportar**: GEDCOM 5.5.1 en UTF-8 con nombres, sexo, fechas de nacimiento y defunción, parejas, hijos y la historia como nota, sin fotos; cada persona lleva su id (`_UID`), así que un árbol que sale de Ramo y vuelve se reconoce y se puede fusionar |
| Colocación del árbol y forma de las tarjetas | Menú ⋮: botones con dibujo, la elegida en dorado (Compacto, Lateral, Balanceado, Escalonado; foto al lado o encima, sin foto; con o sin tarjeta; con o sin apellidos; con o sin los años) |

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

## Proyectos
- `src/ArbolGenealogico.Movil`: la aplicación (Avalonia 12), igual en cualquier sistema. Usa `ArbolGenealogico.Core`
  (modelo, colocación, `.ramo` y fusión).
- `src/ArbolGenealogico.Movil.Android`: lo propio de Android (abrir archivos de otras aplicaciones, «Compartir»,
  cámara). Sin permiso de internet.
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
2. **Crear aplicación**: nombre «Ramo · Árbol genealógico», idioma español, aplicación (no juego), **de pago** si se vende con un
   pago único. Ojo: una aplicación publicada como gratuita no puede pasar a ser de pago después. Si se prefiere una
   versión gratuita con un desbloqueo, se haría con una compra dentro de la aplicación (Play Billing), que se puede
   comprobar en el propio teléfono, sin servidor.
3. **Contenido de la aplicación**:
   - Política de privacidad: `https://thenend.github.io/arbol-genealogico/privacidad-android.html` (está en
     `src/ArbolGenealogico.Web/wwwroot`, se publica con la web).
   - Seguridad de los datos: **no recoge datos** y **no comparte datos** (todo se queda en el teléfono, también la copia
     en Documentos; lo que el usuario manda con «Compartir» lo manda él con otra aplicación).
   - Anuncios: no. Acceso a la aplicación: no hace falta cuenta. Público objetivo: mayores de 18 (o 13+).
   - Clasificación de contenido: cuestionario, todo «no».
4. **Ficha de Play Store** en seis idiomas (textos en `tienda/ficha.md`), icono `tienda/icono-play-512.png`, imagen
   destacada `tienda/destacada/<idioma>.png` y capturas `tienda/capturas/<idioma>/*.png` (1080 × 2160). El icono (el ramo) se genera con
   `dotnet run tienda/ramo.cs -- todo …` (Android, Play, escritorio y web; ver el comentario del principio) y la imagen
   destacada con `tienda/destacada-ramo.cs` (el idioma, el último argumento). Las capturas, con la aplicación de
   escritorio, `ARBOL_ESCALA=2.5`, `ARBOL_IDIOMA` y los pasos `abrir:Ejemplos/familia-garcia.ramo sel:<Padre>`,
   `ejemplo inicio`, `abrir:Ejemplos/familia-perez.ramo recibir:<árbol>`, `abrir:… fusionar:<árbol>|Ana` y
   `abrir:… ficha:<Abuelo paterno>` (con los nombres del ejemplo en ese idioma).
5. **Prueba cerrada**: las cuentas personales creadas después de noviembre de 2023 tienen que hacer una prueba cerrada
   con **al menos 12 personas durante 14 días seguidos** antes de poder publicar. Lo natural es la propia familia: se
   añaden sus correos de Google como testers, aceptan la invitación desde el enlace e instalan la aplicación.
6. Pasados los 14 días, **solicitar acceso a producción** y publicar.

### Textos de la ficha
Están en [tienda/ficha.md](tienda/ficha.md), en los seis idiomas: nombre («Ramo · Árbol genealógico», «Ramo · Family
Tree»…), descripción breve y completa.

**Categoría**: Estilo de vida (o Educación). **Etiquetas**: genealogía, árbol familiar, familia.
