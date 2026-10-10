# Árbol Genealógico en la web

La versión web es el mismo programa que el de escritorio, en el navegador: los mismos algoritmos de colocación
(Compacto, Lateral, Balanceado y Escalonado), las mismas tarjetas y el mismo formato JSON. Además:

- **Los datos están en la nube** (Supabase): se abre desde cualquier ordenador, tablet o móvil.
- **Se comparte con la familia**: cada familiar entra con su correo, y el propietario decide quién **puede editar** y
  quién **solo puede ver**.
- **Guarda sola** al momento. Si otro familiar está editando a la vez, el árbol se actualiza solo con sus cambios. Si los
  dos cambian el árbol al mismo tiempo, la web avisa y deja elegir qué versión se queda.
- **Se pasa del escritorio a la web y al revés**: en «Mis árboles», **Subir archivos .json** acepta varios a la vez
  y conserva los enlaces entre ellos (el icono de enlace de las tarjetas). **Descargar .json** devuelve un archivo que la
  aplicación de escritorio abre tal cual.

Funciona gratis con **GitHub Pages** (la página) y el plan gratuito de **Supabase** (los datos y las cuentas).

## Puesta en marcha (una sola vez, unos 15 minutos)

### 1. Crear la base de datos en Supabase
1. Crea una cuenta en <https://supabase.com> (puedes entrar con tu cuenta de GitHub) y pulsa **New project**. Ponle un
   nombre (por ejemplo `arbol-genealogico`), una contraseña para la base de datos (guárdala, aunque la web no la usa) y
   la región **West EU** (u otra cercana). Espera un par de minutos a que se cree.
2. En el menú de la izquierda, abre **SQL Editor**, pega todo el contenido de [`supabase/esquema.sql`](supabase/esquema.sql)
   y pulsa **Run**. Crea las tablas y los permisos. Se puede volver a ejecutar más adelante (por ejemplo, tras una
   actualización) sin perder nada.

### 2. Configurar las cuentas
En **Authentication**:
1. **Sign In / Providers → Email**: déjalo activado. Recomendado: **desactiva «Confirm email»**. El servicio de correo que
   trae Supabase solo envía unos pocos correos por hora (y a veces acaban en spam). Sin confirmación, los familiares se
   crean la cuenta con correo y contraseña y entran al momento. Si quieres que se confirme el correo, o usar «Entrar sin
   contraseña» y «He olvidado la contraseña» con muchos familiares, configura un servidor de correo propio en
   **Authentication → Emails → SMTP Settings** (sirven Brevo, Resend, Gmail…).
2. **URL Configuration**: en **Site URL** escribe la dirección de la web,
   `https://thenend.github.io/arbol-genealogico/`, y añádela también en **Redirect URLs**. Es adonde llevan los enlaces de
   los correos.

### 3. Copiar las claves de Supabase en GitHub
1. En Supabase, abre **Project Settings → Data API** y copia la **Project URL**. Luego, en **Project Settings → API Keys**,
   copia la clave **anon public**. Esta clave puede estar en la página, porque sin una cuenta con permiso no da acceso a
   nada: los permisos de `esquema.sql` deciden quién ve qué. La clave **service_role** no se usa: no la copies en ningún sitio.
2. En GitHub, abre el repositorio y ve a **Settings → Secrets and variables → Actions → pestaña Variables → New repository
   variable**. Crea dos variables:
   - `SUPABASE_URL`: la Project URL (`https://xxxxxxxx.supabase.co`, sin nada detrás; si copias la que acaba en `/rest/v1/` también vale, la web se queda con la raíz)
   - `SUPABASE_ANON_KEY`: la clave anon public

### 4. Publicar la web
1. En GitHub, **Settings → Pages → Build and deployment → Source**: elige **GitHub Actions**.
2. Pasa los cambios a la rama `main` (fusiona la *pull request* de la rama de la web). Cada vez que cambie la web en `main`,
   la acción **Web** (pestaña **Actions**) la compila y la publica en unos 3 minutos. También se puede lanzar a mano desde
   **Actions → Web → Run workflow**.
3. Abre `https://thenend.github.io/arbol-genealogico/`, crea tu cuenta y sube tus `.json`.

## Compartir con la familia
Abre el árbol y pulsa **Compartir** (en el móvil, en el menú «⋯»). Escribe el correo de cada familiar y elige
**Puede editar** o **Solo ver**. Mándales el enlace de la web: cuando entren con ese correo (creándose la cuenta si no la
tienen), verán el árbol en «Mis árboles». Puedes cambiar los permisos o quitar el acceso cuando quieras.

- Solo el propietario invita, cambia permisos y puede eliminar el árbol. Quien puede editar, edita las personas y el nombre del árbol.
- Al usar **Crear su árbol con su familia…** en una tarjeta, el árbol nuevo se comparte con las mismas personas que el
  actual (el propietario de este pasa a poder editar el nuevo), para que todos puedan seguir el enlace entre los dos.

## Familia y amigos
En «Mis árboles», el botón **Familia y amigos** muestra a todas las personas que usan la web, para encontraros y compartir
los árboles sin tener que escribir correos:

- **Tu nombre**: ponlo arriba para que te reconozcan (si no, se ve tu correo).
- **Ver sus árboles y pedir acceso**: de cada persona se ven los nombres de sus árboles y cuántas personas tienen (no su
  contenido). Con **Pedir acceso** (y un mensaje si quieres) le llega la petición; cuando la acepte, el árbol aparece en tus
  «Mis árboles».
- **Compartir un árbol tuyo** con alguien de la lista: lo ve al momento, sin invitaciones. También desde **Compartir**
  dentro del árbol, escribiendo su nombre o eligiéndolo de la lista.
- **Peticiones que te hacen**: salen arriba en «Familia y amigos», con un aviso en «Mis árboles» y en el botón Compartir
  del árbol. Puedes aceptar (que pueda editar o solo ver) o rechazar.
- **Árboles privados**: en **Compartir**, desmarca «Que la familia vea… que existe este árbol» y deja de aparecer en el
  directorio (quien ya tiene acceso lo sigue teniendo).

Tras actualizar la web con esta función hay que volver a ejecutar `supabase/esquema.sql` en el SQL Editor de Supabase (no
borra nada: solo añade lo nuevo).

## Uso
Es como la aplicación de escritorio (atajos de teclado en el menú «⋯ → Atajos de teclado», o la tecla F1):

| Acción | Ordenador | Móvil o tablet |
|---|---|---|
| Mover / zoom | Arrastrar el fondo, rueda del ratón, botones + − | Un dedo mueve, dos dedos hacen zoom |
| Ver el linaje de alguien | Clic en su tarjeta | Tocar su tarjeta |
| Editar (o ver la ficha) | Doble clic, Intro o F2 | Doble toque |
| Añadir familiares | Botón «+» de la tarjeta, clic derecho, tecla + o Insert | Botón «+» o mantener pulsada la tarjeta |
| Poner foto | Arrastrar la imagen sobre la tarjeta, pegarla (Ctrl+V) o «Poner foto» en el editor | «Poner foto» en el editor (cámara o galería) |
| Buscar | Ctrl+F o el buscador de arriba; Intro pasa a la siguiente | Lupa de arriba |
| Colocación y forma de las tarjetas | Botón de la tarjeta, arriba (Ctrl+L, Ctrl+T…) | El mismo botón |
| Deshacer / rehacer | Ctrl+Z / Ctrl+Y | Menú «⋯» |
| Guardar como imagen, imprimir, descargar .json | Menú «⋯» (Ctrl+E, Ctrl+P) | Menú «⋯» |

Las fotos se guardan reducidas a 320 px dentro del propio árbol, igual que en el escritorio.

**Diferencias con el escritorio.** Imprimir ajusta el árbol a una hoja; para imprimir en varias hojas pegadas, con las guías
de corte, usa la aplicación de escritorio (descarga el .json) o guarda una imagen grande. No hay minimapa.

## Cosas que conviene saber del plan gratuito de Supabase
- **Si nadie entra en la web durante una semana, Supabase pausa el proyecto.** Los datos no se pierden: entra en
  <https://supabase.com/dashboard>, abre el proyecto y pulsa **Restore**. Mientras está en pausa, la web dice
  «Sin conexión con el servidor».
- Caben 500 MB, de sobra para muchos árboles con fotos (cada foto ocupa unos 20–30 KB).
- **Copias de seguridad**: de vez en cuando, descarga los árboles como .json desde «Mis árboles» (menú «⋯» de cada uno).

## Probarla en tu ordenador
Sin configurar nada, la web funciona en **modo de prueba**: los árboles se guardan solo en ese navegador, sin cuentas
ni compartir.
```
dotnet run --project src/ArbolGenealogico.Web
```
Para probarla conectada a Supabase, pon la URL y la clave en `src/ArbolGenealogico.Web/wwwroot/appsettings.json` (sin
subir ese cambio al repositorio) y añade `http://localhost:5000` (o el puerto que indique) a las **Redirect URLs** de Supabase.

## Cómo está hecha
- `src/ArbolGenealogico.Web`: Blazor WebAssembly (.NET 10), que corre entero en el navegador. Usa `ArbolGenealogico.Core`
  sin cambios (modelo, JSON y algoritmos de colocación), así que la web y el escritorio colocan el árbol igual. Las tarjetas
  son HTML y las líneas SVG, con los colores y tamaños de la aplicación de escritorio.
  - `Pages/VerArbol.razor(.cs)`: el árbol (edición, deshacer, guardado, búsqueda, menús…).
  - `wwwroot/js/vista.js`: mover y hacer zoom (ratón, teclado y dedos), fotos, imagen e impresión.
  - `wwwroot/js/datos.js`: Supabase (cuentas, árboles, compartir y tiempo real), o el navegador en modo de prueba.
- `supabase/esquema.sql`: tablas (`arboles` con el JSON de cada árbol, `miembros` e `invitaciones`), permisos por árbol
  (RLS) y funciones (guardar sin pisar los cambios de otro, aceptar invitaciones…).
- `.github/workflows/web.yml`: compila y publica en GitHub Pages.
