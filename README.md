# Árbol Genealógico

Aplicación de escritorio para Windows (WPF / .NET 10) para crear árboles genealógicos. Funciona sin conexión, con tema oscuro, y guarda todo en JSON.

## Ejecutar
- `dist\ArbolGenealogico.exe` (autónomo, no requiere instalar nada).
- También puedes abrir un `.json` arrastrándolo a la ventana, o con `ArbolGenealogico.exe ruta\arbol.json`.
- Ejemplos en `Ejemplos\`: `familia-garcia.json` enlaza con `familia-perez.json` (icono de enlace en la tarjeta de «Tía Rosa»).

## Uso
| Acción | Cómo |
|---|---|
| Mover / zoom | Arrastrar el fondo · rueda del ratón · minimapa (clic o arrastre) |
| Editar persona | Doble clic en la tarjeta (o F2 / Enter) |
| Añadir familiares | Botón **+** de la tarjeta (o clic derecho): padres, hermano/a, pareja, hijo/a |
| Árbol propio de una persona | Menú **+** → «Crear un árbol propio…»; después, icono de enlace de su tarjeta |
| Volver al árbol anterior | Flecha de la barra superior · Alt+← |
| Deshacer / rehacer | Ctrl+Z / Ctrl+Y |
| Guardar / abrir / nuevo | Ctrl+S (Ctrl+Mayús+S «como») · Ctrl+O · Ctrl+N |
| Ver todo · ir a la persona principal | Ctrl+0 · Inicio |

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
ArbolGenealogico.exe --render in.json out.png [escala]   # dibuja un árbol a PNG sin abrir ventana
dotnet publish src/ArbolGenealogico.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```
- `src/ArbolGenealogico.Core`: modelo, JSON y `Layout/` (generaciones → clusters de pareja → orden por filas → coordenadas por relajación con restricciones → aristas ortogonales).
- `src/ArbolGenealogico.App`: interfaz WPF.
