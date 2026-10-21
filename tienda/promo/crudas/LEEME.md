# Capturas crudas para las imágenes promocionales

Pantallas de la aplicación del móvil sin marco, para enmarcarlas después como imágenes de Google Play. Salen de la
ventana de escritorio (`src/ArbolGenealogico.Movil.Escritorio`, modo `--captura`) con el árbol de ejemplo
`Ejemplos/familia-garcia.ramo`, traducido al abrirlo (Smith Family, Familie Müller, Famiglia Rossi…).

```
dotnet build src/ArbolGenealogico.Movil.Escritorio -c Release
python tienda/promo/crudas/capturar.py                 # todo; o p. ej. «capturar.py es --equipo tablet --solo a,e»
```

Cada captura usa una carpeta de datos nueva (`ARBOL_DATOS`), `ARBOL_IDIOMA=<idioma>`, `ARBOL_LETRA=1` y
`ARBOL_COMPLETA=1` (versión completa: sin «Hecho con Ramo» ni límites).

- `telefono/<idioma>/`: ventana de 432 × 864 con `ARBOL_ESCALA=2.5` → 1080 × 2160. Además `pdf.pdf` y `imagen.png`
  (lo que mandan «Compartir como PDF» y «Compartir como imagen»), por si sirven de dibujo.
- `tablet/<idioma>/`: ventana de 1280 × 720 con `ARBOL_ESCALA=2` → 2560 × 1440 (16:9).

| Archivo | Pantalla | Pasos (`<Padre>`, `<Abuelo paterno>` en el idioma de la captura) |
|---|---|---|
| `a-arbol.png` | el árbol con la línea de «Padre» iluminada | `abrir:familia-garcia.ramo sel:<Padre> zoom:0.85` (tableta: `zoom:1.1 mover:0,-60`) |
| `b-familiar.png` | «Añadir familiar de…» | lo de `a` + `familiar:<Padre>` |
| `c-compartir.png` | el menú: compartir el .ramo, imprimir, PDF, imagen, GEDCOM | lo de `a` + `menu` |
| `d-imprimir.png` | imprimir, en 2 hojas A4 | `abrir:… imprimir imprimir:hojas=2` |
| `e-fusion.png` | fusionar con «El árbol de Ana» | `abrir:… fusionar:<variante>\|Ana` |
| `f-ficha.png` | la ficha del abuelo, con su historia | `abrir:… ficha:<Abuelo paterno>` |
| `g-buscar.png` | buscar por apellido, con resultados | `abrir:… buscar:Garc` (Smi, Mar, Ros, Mül, Sil; tableta: `zoom:1.2 mover:0,-180`) |
| `h-estilos-vertical.png` | el árbol con tarjetas verticales sin foto | `abrir:… esperar:600 forma:vertical,sin-foto sel:<Padre> escala:0.75` (tableta: `escala:0.95 mover:0,-180`) |
| `h-estilos-nombre.png` | solo nombre, apellidos y años, sin foto ni tarjeta | `abrir:… esperar:600 forma:sin-foto,sin-tarjeta sel:<Padre> escala:0.8` (tableta: `escala:1 mover:0,-110`) |

La variante de `e` es el mismo árbol con una nieta y la pareja de «Hijo 1» de más, la fecha entera de nacimiento de
«Madre» y otro año para «Padre»: así la pantalla dice «31 personas en los dos árboles · se añadirán 2 · se completarán
datos de 1» y pregunta qué dato se queda. `capturar.py` la escribe en una carpeta temporal.

`enmarcar.py` junta las dos `h-estilos` en una sola imagen («Tarjetas a tu gusto»).

Las fotos de las personas son las de `Ejemplos/fotos` (ver `tienda/promo/fotos-ejemplo.py`).

En la tableta, la fusión y la ficha son una columna centrada (como en la aplicación) con fondo vacío a los lados.
