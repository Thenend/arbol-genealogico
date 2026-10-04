# Árbol Genealógico

Aplicación de escritorio para Windows (WPF / .NET 10) para crear árboles genealógicos. Funciona sin conexión, con tema oscuro, y guarda todo en JSON.

## Ejecutar
- `dist\ArbolGenealogico.exe` (autónomo, no requiere instalar nada).
- También puedes abrir un `.json` arrastrándolo a la ventana, o con `ArbolGenealogico.exe ruta\arbol.json`.
- Al abrirse, la aplicación vuelve a como la dejaste: el último árbol, la ventana en el mismo sitio y tamaño, y la misma vista (zoom, zona del árbol y persona seleccionada); cada árbol recuerda su propia vista. La primera vez que se abre un árbol se ve entero si cabe con un zoom legible; si no, la persona principal con su familia más cercana.
- Ejemplos en `Ejemplos\`: `familia-garcia.json` enlaza con `familia-perez.json` (icono de enlace en la tarjeta de «Tía Rosa»).

## Uso
| Acción | Cómo |
|---|---|
| Mover / zoom | Arrastrar el fondo · rueda del ratón · minimapa (clic o arrastre) · Ctrl+flechas · Ctrl + / Ctrl − |
| Ver el linaje de alguien | Clic en su tarjeta: se iluminan en turquesa las líneas hacia sus padres, abuelos… y hacia sus hijos, nietos…, y se oscurece todo lo que no es de su familia directa (se mantienen a plena luz sus antepasados, descendientes, pareja y hermanos, y también las líneas que llevan hasta ellos). Pulsa el fondo o Esc para volver a verlo todo |
| Buscar personas | **Ctrl+F** o el campo de la barra superior: escribe parte del nombre o los apellidos (sin importar mayúsculas ni tildes; varias palabras en cualquier orden). Indica cuántas coincidencias hay y las marca con un anillo verde lima. **Intro** (o F3) pasa a la siguiente, **Mayús+Intro** a la anterior: la selecciona y mueve la vista hasta ella. Esc borra la búsqueda |
| Moverse por el árbol con el teclado | Flechas: ←→ en la misma fila, ↑ a un padre, ↓ a un hijo (sin selección, la primera flecha elige a la persona principal) |
| Editar persona | Doble clic en la tarjeta (o Intro / F2). En el diálogo, Tab o ↑ ↓ cambian de campo hasta llegar a Guardar (en la historia, ↑ ↓ mueven el cursor y solo pasan de campo desde su primera o última línea), ← → eligen el sexo o cambian entre Guardar y Cancelar; Intro guarda |
| Añadir familiares | Botón **+** de la tarjeta (o clic derecho), o con el teclado **Insert** / **+**: padres, hermano/a, pareja, hijo/a. Se elige con ↑↓ + Intro o pulsando el número |
| Poner foto | Arrastra una imagen desde el Explorador de Windows (o desde el navegador) sobre la tarjeta: se ilumina al pasar por encima. O selecciona la persona y pulsa **Ctrl+V** con una imagen (o un archivo de imagen) en el portapapeles. También funciona arrastrando o pegando dentro del editor. Se respeta la orientación de las fotos de móvil y se guardan reducidas a 320 px dentro del propio JSON |
| Eliminar | **Supr**: pide confirmación (Intro confirma, Esc cancela) y se puede deshacer con Ctrl+Z |
| Árbol propio de una persona | Menú **+** o botón «Crear su árbol con su familia…» del editor de la tarjeta (no para la persona principal: su árbol es el actual): crea un árbol nuevo con esa persona como principal, llevándose una copia de todos sus familiares de sangre de este árbol (antepasados, hermanos, tíos, primos, sobrinos, descendientes…) y de los cónyuges de todos ellos; la familia política no se lleva. Comprueba que lo actual esté guardado, pide dónde guardar el archivo nuevo, enlaza las tarjetas (en este árbol, la de la persona con el árbol nuevo; en el nuevo, la del dueño de este árbol, si viaja, con este árbol), cierra el árbol actual y abre el nuevo. El icono de enlace de la tarjeta o Ctrl+Intro saltan al otro árbol |
| Saltar entre árboles | Icono de enlace de la tarjeta o Ctrl+Intro. Si el archivo no está donde se esperaba, ofrece buscarlo; al pasar a otro árbol, si el actual tiene cambios (por ejemplo el enlace recién localizado) pregunta si guardarlos, como al abrir otro árbol con «Abrir» |
| Volver al árbol anterior | Flecha de la barra superior · Alt+← |
| Deshacer / rehacer | Ctrl+Z / Ctrl+Y |
| Guardar / abrir / nuevo | Ctrl+S (Ctrl+Mayús+S «como») · Ctrl+O · Ctrl+N |
| Ver todo · ir a la persona principal | Ctrl+0 · Inicio |
| Cambiar la colocación del árbol | Selector **Compacto / Lateral / Balanceado / Escalonado** de la barra superior, o **Ctrl+L** (pasa a la siguiente). Las tarjetas se deslizan a su nuevo sitio, la persona seleccionada (o, sin selección, la más cercana al centro de la pantalla) se queda donde estaba (si estabas viendo el árbol entero sin nadie seleccionado, se ajusta para verlo entero también en la nueva) y la elección se recuerda para la próxima vez. La primera vez se usa **Balanceado** |
| Quitar la selección · ver todos los atajos | Esc · F1 |

**Línea sanguínea y política.** Se considera sanguínea a la persona principal, sus antepasados y los descendientes de estos (hermanos, tíos, primos…). Las parejas de esas personas son *políticas* (borde discontinuo): se muestran, pero su familia no se añade aquí, sino en su propio árbol enlazado. La línea directa (tú → padres → abuelos…) se resalta en dorado.

**Colores de las tarjetas.** Azul (esquinas rectas) para los hombres, rosa (muy redondeadas) para las mujeres y violeta (intermedias) si el sexo no está especificado. Las personas sin nombre se ven en gris, conservando la forma de su sexo.

**Ordenación Compacto.** Es el mismo esquema que los árboles impresos: cada persona aparece una sola vez y, cuando las ramas de los dos miembros de una pareja se encuentran con primos, tíos y sobrinos de por medio, la pareja **baja** hasta una fila por debajo de todos los descendientes de sus hermanos. El hilo desde sus padres cae por el lateral de la rama (puede ser una línea larga) y todo el espacio que queda debajo es de la pareja y sus hijos. Si las dos tarjetas de la pareja no pueden ir pegadas (cada una cuelga de una columna distinta con otras familias entre medias), se **separan horizontalmente** y su línea de pareja las une por el hueco libre de su fila; los hijos cuelgan de esa línea. Las líneas no se cruzan y las tarjetas no se solapan; en árboles con varios matrimonios por persona o muy enrevesados (primos que se casan entre sí…) puede quedar algún cruce.

**Ordenación Lateral.** Es la colocación de la app *Family TreePhoto*. El árbol se construye desde la persona principal hacia arriba y cada antepasado es el pie de un bloque con sus padres, sus hermanos (con toda su descendencia) y él mismo, que baja en línea recta desde el punto de unión de sus padres hasta quedar por debajo del más profundo de sus sobrinos. En cada pareja de antepasados la mujer va a la izquierda y el hombre a la derecha, cada uno al pie de su propio bloque, con el hueco de una tarjeta entre los dos bloques del que cuelgan sus hijos. El resto de las familias se dibujan como un árbol clásico (padres centrados sobre sus hijos) y todo se empaqueta lo más junto posible, de modo que las generaciones de ramas distintas no tienen por qué estar a la misma altura. Los hermanos salen en el orden en que están guardados en el árbol.

**Ordenación Balanceado.** Igual que la Lateral, con dos cambios: los hermanos de cada antepasado se reparten a los dos lados de su línea (en su orden, de modo que los dos lados queden más o menos igual de anchos) y, en cada pareja de antepasados, la rama con más familia va por fuera del árbol, aunque eso deje a la mujer a la derecha. Así los cónyuges de la línea directa quedan mucho más cerca entre sí y de sus hijos. Además, cuando hay sitio, los hijos de cada pareja de antepasados se centran bajo la línea que los une (sin alejar a cada uno de su propia pareja).

**Ordenación Escalonado.** Pensada para imprimir el árbol en una hoja: es la Balanceado, pero tan estrecha como se pueda. Cuando varias familias cuelgan de la misma línea (los hermanos de un antepasado o los hijos de una pareja), las de fuera quedan arriba y cada una más hacia dentro baja unas filas, con su línea larga, para meter su descendencia por debajo de la de al lado, en escalera. De las ramas de los dos miembros de una pareja de antepasados, una puede subir por encima de la otra en vez de ir a su lado. Se prueban varios grados de escalonado y se elige el que deja el árbol más grande al imprimirlo en un A4 (apaisado o vertical).

## Formato JSON
```json
{ "version": 1, "nombre": "Familia García", "raizId": "p1",
  "personas": [ { "id": "p1", "nombre": "", "apellidos": "", "sexo": "M|F|U",
                  "foto": "<base64 JPEG, opcional>", "historia": "", "arbolEnlazado": "otro.json" } ],
  "uniones":  [ { "id": "u1", "parejas": ["p2", "p3"], "hijos": ["p1", "p4"] } ] }
```

## Desarrollo
```
dotnet test                                   # modelo, JSON y algoritmo de colocación (familias aleatorias)
dotnet run --project src/ArbolGenealogico.App
ArbolGenealogico.exe --render in.json out.png [escala] [idPersona|-] [A|B|C|D]   # dibuja un árbol a PNG sin abrir ventana (con linaje de esa persona y la ordenación indicada; por defecto C, Balanceado)
dotnet publish src/ArbolGenealogico.App -c Release -r win-x64 --self-contained -o dist   # un solo .exe comprimido (~60 MB), sin necesidad de instalar .NET
```
- `src/ArbolGenealogico.Core`: modelo, JSON y `Layout/` (generaciones → clusters de pareja → orden por filas → coordenadas por relajación con restricciones → aristas ortogonales).
- `src/ArbolGenealogico.App`: interfaz WPF.
