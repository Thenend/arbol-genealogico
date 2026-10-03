# Árbol Genealógico

Aplicación de escritorio para Windows (WPF / .NET 10) para crear árboles genealógicos. Funciona sin conexión, con tema oscuro, y guarda todo en JSON.

## Ejecutar
- `dist\ArbolGenealogico.exe` (autónomo, no requiere instalar nada).
- También puedes abrir un `.json` arrastrándolo a la ventana, o con `ArbolGenealogico.exe ruta\arbol.json`.
- Ejemplos en `Ejemplos\`: `familia-garcia.json` enlaza con `familia-perez.json` (icono de enlace en la tarjeta de «Tía Rosa»).

## Uso
| Acción | Cómo |
|---|---|
| Mover / zoom | Arrastrar el fondo · rueda del ratón · minimapa (clic o arrastre) · Ctrl+flechas · Ctrl + / Ctrl − |
| Ver el linaje de alguien | Clic en su tarjeta: se iluminan en turquesa las líneas hacia sus padres, abuelos… y hacia sus hijos, nietos… |
| Moverse por el árbol con el teclado | Flechas: ←→ en la misma fila, ↑ a un padre, ↓ a un hijo (sin selección, la primera flecha elige a la persona principal) |
| Editar persona | Doble clic en la tarjeta (o Intro / F2). En el diálogo, Tab cambia de campo y ← → eligen el sexo; Intro guarda |
| Añadir familiares | Botón **+** de la tarjeta (o clic derecho), o con el teclado **Insert** / **+**: padres, hermano/a, pareja, hijo/a. Se elige con ↑↓ + Intro o pulsando el número |
| Poner foto | Arrastra una imagen desde el Explorador de Windows (o desde el navegador) sobre la tarjeta: se ilumina al pasar por encima. O selecciona la persona y pulsa **Ctrl+V** con una imagen (o un archivo de imagen) en el portapapeles. También funciona arrastrando o pegando dentro del editor. Se respeta la orientación de las fotos de móvil y se guardan reducidas a 320 px dentro del propio JSON |
| Eliminar | **Supr**: pide confirmación (Intro confirma, Esc cancela) y se puede deshacer con Ctrl+Z |
| Árbol propio de una persona | Menú **+** → «Crear un árbol propio…»; después, icono de enlace de su tarjeta o Ctrl+Intro |
| Volver al árbol anterior | Flecha de la barra superior · Alt+← |
| Deshacer / rehacer | Ctrl+Z / Ctrl+Y |
| Guardar / abrir / nuevo | Ctrl+S (Ctrl+Mayús+S «como») · Ctrl+O · Ctrl+N |
| Ver todo · ir a la persona principal | Ctrl+0 · Inicio |
| Quitar la selección · ver todos los atajos | Esc · F1 |

**Línea sanguínea y política.** Se considera sanguínea a la persona principal, sus antepasados y los descendientes de estos (hermanos, tíos, primos…). Las parejas de esas personas son *políticas* (borde discontinuo): se muestran, pero su familia no se añade aquí, sino en su propio árbol enlazado. La línea directa (tú → padres → abuelos…) se resalta en dorado.

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
ArbolGenealogico.exe --render in.json out.png [escala] [idPersona]   # dibuja un árbol a PNG sin abrir ventana (con linaje de esa persona)
dotnet publish src/ArbolGenealogico.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```
- `src/ArbolGenealogico.Core`: modelo, JSON y `Layout/` (generaciones → clusters de pareja → orden por filas → coordenadas por relajación con restricciones → aristas ortogonales).
- `src/ArbolGenealogico.App`: interfaz WPF.
