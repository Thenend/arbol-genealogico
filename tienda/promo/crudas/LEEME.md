# Capturas crudas para las imágenes promocionales

Pantallas de la aplicación del móvil sin marco, para enmarcarlas después como imágenes de Google Play. Salen de la
ventana de escritorio (`src/ArbolGenealogico.Movil.Escritorio`, modo `--captura`) con el árbol de ejemplo
`Ejemplos/familia-garcia.ramo`, traducido al abrirlo (Smith Family, Familie Müller, Famiglia Rossi…).

```
dotnet build src/ArbolGenealogico.Movil.Escritorio -c Release
python tienda/promo/crudas/capturar.py                 # todo; o p. ej. «capturar.py es --equipo tablet --solo a,e»
```

En Linux (también en la nube) sale igual con una pantalla virtual: `xvfb-run -a -s "-screen 0 2560x1440x24" python3
tienda/promo/crudas/capturar.py es`. Las capturas usan el tema de la primera vez, Jardín, salvo las de `h-aspecto`.

Cada captura usa una carpeta de datos nueva (`ARBOL_DATOS`), `ARBOL_IDIOMA=<idioma>`, `ARBOL_LETRA=1` y
`ARBOL_COMPLETA=1` (versión completa: sin «Hecho con Ramo» ni límites).

- `telefono/<idioma>/`: ventana de 432 × 864 con `ARBOL_ESCALA=2.5` → 1080 × 2160. Además `pdf.pdf` y `imagen.png`
  (lo que mandan «Compartir como PDF» y «Compartir como imagen»), por si sirven de dibujo.
- `tablet/<idioma>/`: ventana de 1280 × 720 con `ARBOL_ESCALA=2` → 2560 × 1440 (16:9).

| Archivo | Pantalla | Pasos (con los nombres del ejemplo en el idioma de la captura: Javier, Beatriz, Dolores…) |
|---|---|---|
| `a-arbol.png` | el árbol con la línea de Javier iluminada (la única con alguien seleccionado) | `abrir:familia-garcia.ramo sel:<Javier> zoom:0.85` (tableta: `zoom:1.1 mover:0,-60`) |
| `b-familiar.png` | «Añadir familiar de Beatriz» | `abrir:… sel:<Beatriz> zoom:0.85 familiar:<Beatriz>` |
| `c-compartir.png` | el menú: compartir el .ramo, imprimir, PDF, imagen, GEDCOM | `abrir:… todo escala:0.45 menu` (tableta: `escala:0.6`) |
| `d-imprimir.png` | imprimir, en 2 hojas A4 | `abrir:… imprimir imprimir:hojas=2` |
| `e-fusion.png` | fusionar con «El árbol de Ana» | `abrir:… fusionar:<variante>\|Ana` |
| `f-ficha.png` | la ficha de la abuela Dolores, con su foto antigua | `abrir:… ficha:<Dolores>` |
| `g-buscar.png` | buscar por apellido, con resultados | `abrir:… buscar:Lóp` (las tres primeras letras de López en cada idioma; tableta: `zoom:1.2 mover:0,-180`) |
| `h-aspecto-foto.png` | compacto, en vertical, con foto y sin tarjeta, tema Álbum | `abrir:… tema:album orden:A forma:vertical,sin-tarjeta todo escala:0.5 mover:0,-40` (tableta: `escala:0.6`) |
| `h-aspecto-nombre.png` | balanceado, solo con el nombre, tema Día | `abrir:… tema:dia orden:C forma:sin-foto,sin-tarjeta,sin-apellidos,sin-fechas todo escala:0.72` (tableta: `escala:0.8`) |

La variante de `e` es el mismo árbol con una nieta y la pareja de Daniel de más, la fecha entera de nacimiento de
Mercedes y otro año para Antonio: así la pantalla dice «31 personas en los dos árboles · se añadirán 2 · se completarán
datos de 1» y pregunta qué dato se queda. `capturar.py` la escribe en una carpeta temporal.

`enmarcar.py` junta las dos `h-aspecto` en una sola imagen («Tu árbol, a tu manera»). No se usa en ninguna
la forma sin foto ni tarjeta con apellidos y años: queda peor.

Las fotos de las personas son las de `Ejemplos/fotos` (ver `tienda/promo/fotos-ejemplo.py`).

En la tableta, la fusión y la ficha son una columna centrada (como en la aplicación) con fondo vacío a los lados.
